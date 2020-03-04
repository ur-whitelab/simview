from rdkit import Chem, RDConfig
from rdkit.Chem import AllChem, rdMolAlign
from rdkit.Chem import Draw
import hoomd, hoomd.md
import hoomd.hzmq
import math, numpy as np, gsd, gsd.hoomd, mbuild as mb, pickle
import os, fire
from mbuild.utils.sorting import natural_sort
from mbuild.utils.conversion import RB_to_OPLS

def run(smiles_string, socket=None, period = 1, temperature = 77, pressure = 1, density = None, epsilon = 1.0, sigma = 1.0, particle_number=10000, steps=1e6, aspect_ratio=16 / 9, filename = None):

    def kT2F(kt):
        # correct for dof
        return (kt / (1.987 * 10**(-3) * 2 / 3) - 273) * 9 / 5 + 32
    def F2kT(t):
        return ((t - 32) * 5/9 + 273) * 1.987 * 10**(-3) * 2 / 3
    def particles2mols(n):
        return n / 6.022 / 10**23
    def v2m3(v):    #nm,to m^3
        return max(1e-30, v /  10**(-30))
    def p2atm(p):
        return p * 4.184 * 6.022 * 10**23 / v2m3(1) * 101325 # kcal/mol/m^3 to atm

    #takes in a SMILES molecule
    mol = Chem.MolFromSmiles(smiles_string) 
    #adds hydrogen the the molecule
    mol_w_hydrogens = Chem.AddHs(mol) 
    AllChem.EmbedMolecule(mol_w_hydrogens)
    #converts the SMILES molecule to a pdb file
    Chem.MolToPDBFile(mol_w_hydrogens, smiles_string+'.pdb') 
    #draw out the molecule as a diagnostic tool
    Draw.MolToFile(mol,'{}/smilesim.png'.format(os.getcwd()))

    #location of the directory that this script is in
    result_dir ='{}'.format(os.getcwd())
    os.makedirs(result_dir, exist_ok=True)

    #the following function takes in the pdb file created above
    class Molecule(mb.Compound):
        def __init__(self,N):
            super(Molecule, self).__init__()
            mb.load(smiles_string +'.pdb', compound=self)
            self.translate(-self[0].pos)

    glc_sys = Molecule(1)
    sys = mb.fill_box(glc_sys, n_compounds = particle_number, density= density, edge = 0.25)
    sys.translate(-sys.pos)
    boxd = sys.periodicity[0] #nm
    box = mb.Box(mins=3 * [-boxd / 2], maxs = 3 * [boxd / 2])
    sys.save(smiles_string+'.gsd', overwrite=True)
    print('box size is:{}'.format(box))

    # Prepare FF for the system
    param_sys, kwargs = prepare_hoomd(sys, forcefield_files=['oplsaa.xml'], forcefield_debug=False, box=box)
    mb.formats.gsdwriter.write_gsd(param_sys, smiles_string+'.gsd', shift_coords=True, **kwargs)
    with open(result_dir + 'model.p', 'wb') as f:
        pickle.dump(param_sys, f)

    #Need to edit to add special pairs
    g = gsd.hoomd.open(smiles_string + '.gsd')
    frame = g[0]
    rcut = 10

    c = hoomd.context.initialize('')
    system = hoomd.init.read_gsd(filename = smiles_string + '.gsd')

    nlist = hoomd.md.nlist.cell(r_buff=0.001, check_period=1)
    pair_coeffs(frame, param_sys, nlist)

    #set-up pppm
    charged = hoomd.group.all();
    pppm = hoomd.md.charge.pppm(nlist=nlist, group=charged)
    pppm.set_params(Nx=32, Ny=32, Nz=32, order=6, rcut=rcut)

    #set-up bonds
    bond_coeffs(frame, system, param_sys)

    #set-up angles
    angle_coeffs(frame, param_sys)

    #set-up dihedrals
    dihedral_coeffs(frame, param_sys)

    #free particles from rigid bodies since rigid doesn't quite work for us
    for i in range(frame.particles.N):
        system.particles[i].body = -1
    group_all = hoomd.group.all()
    #time 1 = 48.9 fs
    #emin
    kT = F2kT(temperature) # 1.9872 / 1000
    # init_dump = hoomd.dump.gsd(filename= 'init.gsd', period=1, group=group_all, phase=0, overwrite=True)

    state_vars = [('temperature', kT2F), ('volume', v2m3), ('num_particles', particles2mols), ('pressure', p2atm), ('lx', lambda x: x), ('ly', lambda x: x), ('lz', lambda x:x)]

    log = hoomd.analyze.log(filename=None, quantities=state_vars, period=period // 10)

    #bonds are constrained, so can use 2 ps
    #NOT ANYMORE
    hoomd.md.integrate.mode_standard(dt=0.4 / 48.9)
    nvt_dump = hoomd.dump.gsd(filename= smiles_string +  '_trajectory.gsd', period=1, group=group_all, phase=0, overwrite=True)
    nvt = hoomd.md.integrate.nvt(group=hoomd.group.all(), kT=kT, tau=0.15)
    nvt.randomize_velocities(1)

    state_vars = [('temperature', kT2F), ('volume', v2m3), ('num_particles', particles2mols), ('pressure', p2atm), ('lx', lambda x: x), ('ly', lambda x: x), ('lz', lambda x:x)]

    log = hoomd.analyze.log(filename=None, quantities=[k for k,_ in state_vars], period=1) #period // 10)

    def callback(sys):
        result = {k: f(log.query(k)) for k,f in state_vars}
        result['density'] = result['num_particles'] / result['volume']
        return result

    def set_callback(**data):
        print(data)
        if 'temperature' in data:
            print('temperature', data['temperature'])
            nvt.set_params(kT = max(0.001,float(data['temperature'])))
        if 'pressure' in data:
            print('pressure', data['pressure'])
            #npt.set_params(P = float(data['pressure']))
        if 'box' in data:
            scale = float(data['box'])
            print('Resizing to', scale * log.query('lx') , ' x ', scale * log.query('lz'))
            if scale > 1:
                hoomd.update.box_resize(Lx=scale * log.query('lx') , Ly=scale * log.query('ly'), Lz=scale * log.query('lz'), period=None, scale_particles=False)
            else:
                hoomd.update.box_resize(Lx=scale * log.query('lx') , Ly=scale * log.query('ly'), Lz=scale * log.query('lz'), period=None, scale_particles=True)
        print(data)

    #equilibrate
   # hoomd.run(1e3)
    hoomd.hzmq.hzmq(socket, period=period, message_size=400, state_callback=callback, set_state_callback=set_callback)
    c.sorter.disable()
    hoomd.run(steps)

if __name__ == '__main__':
    fire.Fire(run)


#hoomd ff file
#Setting up the forcefield
def prepare_hoomd(compound,show_ports=False, forcefield_name=None,
             forcefield_files=None, forcefield_debug=False, box=None,
             overwrite=False, residues=None, references_file=None,
             combining_rule='lorentz', **kwargs):
    structure = compound.to_parmed(box=box, residues=residues)
    # Apply a force field with foyer if specified
    if forcefield_name or forcefield_files:
        from foyer import Forcefield
        ff = Forcefield(forcefield_files=forcefield_files,
                        name=forcefield_name, debug=forcefield_debug)
        structure = ff.apply(structure, use_residue_map=False, references_file=references_file, maxiter=20, switchDistance=None,  **kwargs)
        structure.combining_rule = combining_rule
 
    total_charge = sum([atom.charge for atom in structure])
    if round(total_charge, 4) != 0.0:
        print('System is not charge neutral. Total charge is {}.'
             ''.format(total_charge))
 
    # Provide a warning if rigid_ids are not sequential from 0
    if compound.contains_rigid:
        unique_rigid_ids = sorted(set([p.rigid_id
                                       for p in compound.rigid_particles()]))
        if max(unique_rigid_ids) != len(unique_rigid_ids) - 1:
            print("Unique rigid body IDs are not sequential starting from zero.")
    gsd_kwargs = dict()
    gsd_kwargs['rigid_bodies'] = [p.rigid_id for p in compound.particles()]
    return structure, gsd_kwargs
 
def insert_specials(frame, param_sys):
    pair_types = []
    pair_typeid = []
    pairs = []
    def to_pair_str(t1, t2):
        return '-'.join(sorted([t1, t2], key=natural_sort))
    for ai in param_sys.atoms:
        for aj in ai.dihedral_partners:
            #make sure we don't double add
            if ai.idx > aj.idx:
                ps = to_pair_str(ai.type, aj.type)
                if ps not in pair_types:
                    pair_types.append(ps)
                pair_typeid.append(pair_types.index(ps))
                pairs.append((ai.idx, aj.idx))
    frame.pairs.types = pair_types
    frame.pairs.typeid = pair_typeid
    frame.pairs.group = pairs
    frame.pairs.N = len(pairs)
 
def pair_coeffs(system, param_sys, nlist, lj_fudge = 0.5, coul_fudge = 0.5):
    nlist.reset_exclusions(['1-2', '1-3', '1-4', 'body'])
    hoomd_lj = hoomd.md.pair.force_shifted_lj(r_cut=10.0, nlist=nlist)
 
    # set-up 1,4 special pair interactions
    if system.pairs.types:
        hoomd_special_coul = hoomd.md.special_pair.coulomb()
        hoomd_special_lj = hoomd.md.special_pair.lj()
    unique_types = [t for t in system.particles.types]
    epsilon_dict = dict(zip(unique_types, len(unique_types) * [0]))
    sigma_dict = dict(zip(unique_types, len(unique_types) * [0]))
    for atom in param_sys.atoms:
        epsilon_dict[atom.type] = atom.epsilon
        sigma_dict[atom.type] = atom.sigma
    print(atom.uepsilon)
    print(atom.usigma)
    for i, t1 in enumerate(unique_types):
        for t2 in unique_types[i:]:
            hoomd_lj.pair_coeff.set(t1, t2, epsilon=math.sqrt(epsilon_dict[t1] * epsilon_dict[t2]), sigma=math.sqrt(sigma_dict[t1] * sigma_dict[t2]))
            print(t1, t2, math.sqrt(epsilon_dict[t1] * epsilon_dict[t2]), math.sqrt(sigma_dict[t1] * sigma_dict[t2]))
    #specials now
    pair_types = system.pairs.types
    for t in pair_types:
        t1,t2 = t.split('-')
        print(t)
        hoomd_special_lj.pair_coeff.set(t,
                            epsilon=lj_fudge * math.sqrt(epsilon_dict[t1] * epsilon_dict[t2]),
                            sigma=math.sqrt(sigma_dict[t1] * sigma_dict[t2]),
                            r_cut = 10.0)
        #unclear how pppm is affected by this
        if hoomd_special_coul is not None:
            hoomd_special_coul.pair_coeff.set(t, alpha=coul_fudge, r_cut=2.0)
 
def bond_coeffs(system, hoomd_sim_system, param_sys,  constrain_hydrogens=True):
    bond_types = dict()
    for b in system.bonds.types:
        bond_types[b] = [0,0]
    for b in param_sys.bonds:
        for bk,bv in bond_types.items():
            #print('type bv', type(bv))
            if bv[0] != 0:
                continue #already typed
            a1, a2 = bk.split('-')
            if (a1 == b.atom1.type and a2 == b.atom2.type) or (a2 == b.atom1.type and a1 == b.atom2.type):
                print('b type k',b)

                bv[0] = b.type.k
                bv[1] = b.type.req
                break
    bonds = hoomd.md.bond.harmonic(name="harmonic_bonds")
    print(b.type.uk)
    print(b.type.ureq)
    for bk,bv in bond_types.items():
        bonds.bond_coeff.set(bk, k=bv[0] * 2, r0=bv[1])
        print(bk, *bv)
 
    if constrain_hydrogens:
        #set-up hydrogen covalent bond constraints
        for i in range(system.bonds.N):
            bi, bj = system.bonds.group[i]
            if system.particles.body[bi] == system.particles.body[bj]:
                hoomd_sim_system.constraints.add(bi, bj,bond_types[system.bonds.types[system.bonds.typeid[i]]][1])
 
def angle_coeffs(system, param_sys):
    def get_angle_str(angle):
        t1, t2, t3 = angle.atom1.type, angle.atom2.type, angle.atom3.type
        t1, t3 = sorted([t1, t3], key=natural_sort)
        angle_type = ('-'.join((t1, t2, t3)))
        return angle_type
 
    angle_types = dict()
    for a in system.angles.types:
        angle_types[a] = [0,0]
    for a in param_sys.angles:
        for ak,av in angle_types.items():
            if av[0] != 0:
                continue #already typed
            angle_type_str = get_angle_str(a)
            if angle_type_str == ak:
                av[0] = a.type.k
                av[1] = a.type.theteq
                break
    angles = hoomd.md.angle.harmonic()
    print(a.type.uk)
    print(a.type.utheteq)
    for ak,av in angle_types.items():
        angles.angle_coeff.set(ak, k=av[0] * 2, t0=av[1]  * math.pi / 180)
        print(ak, av[0] * 2, av[1]  * math.pi / 180)
 
def dihedral_coeffs(system, param_sys):
    def get_dihedral_str(dihedral):
        t1, t2 = dihedral.atom1.type, dihedral.atom2.type
        t3, t4 = dihedral.atom3.type, dihedral.atom4.type
        if [t2, t3] == sorted([t2, t3], key=natural_sort):
            dihedral_type = ('-'.join((t1, t2, t3, t4)))
        else:
            dihedral_type = ('-'.join((t4, t3, t2, t1)))
        return dihedral_type
 
    if not system.dihedrals.types:
        return
 
    dihedral_types = dict()
    for d in system.dihedrals.types:
        dihedral_types[d] = None
    for d in param_sys.rb_torsions:
        for dk,dv in dihedral_types.items():
            if dv is not None:
                continue #already typed
            dihedral_str = get_dihedral_str(d)
            if dihedral_str == dk:
                dihedral_types[dk] = [d.type.c0,d.type.c1,d.type.c2,d.type.c3,d.type.c4,d.type.c5,]
                break
    dihedrals = hoomd.md.dihedral.opls()
    print(d.type.uc0)
    for dk,dv in dihedral_types.items():
        dihedrals.dihedral_coeff.set(dk, **{'k{}'.format(i+1):v for i,v in enumerate(RB_to_OPLS(*dv))})
        print(dk, *dv)

#end of hoomdff file

