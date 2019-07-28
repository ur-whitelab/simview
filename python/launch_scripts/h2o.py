import hoomd
import hoomd.md
import hoomd.hzmq
from mbuild.lib.moieties import H2O
import math, numpy as np, gsd, mbuild as mb, pickle
import hoomd_ff


#from utils.py in hoomd-tf
def find_molecules(system):
    '''Given a hoomd system, this will return a mapping
        from molecule index to particle index
        This is a slow function and should only be called once.
    '''
    mapping = []
    mapped = set()
    N = len(system.particles)
    unmapped = set(range(N))
    pi = 0

    # copy over bonds for speed
    bonds = [[b.a, b.b] for b in system.bonds]

    num_bonds = 0
    for b in system.bonds:
        num_bonds += 1
    print("num bonds in python: " + str(num_bonds))

    print('Finding molecules...', end='')
    while len(mapped) != N:
        print('\rFinding molecules...{:.2%}'.format(len(mapped) / N), end='')
        pi = unmapped.pop()
        mapped.add(pi)
        mapping.append([pi])
        # traverse bond group
        # until no more found
        # Have to keep track of "to consider" for branching molecules
        to_consider = [pi]
        while len(to_consider) > 0:
            pi = to_consider[-1]
            found_bond = False
            for bi, bond in enumerate(bonds):
                # see if bond contains pi and an unseen atom
                if (pi == bond[0] and bond[1] in unmapped) or \
                        (pi == bond[1] and bond[0] in unmapped):
                    new_pi = bond[0] if pi == bond[1] else bond[1]
                    unmapped.remove(new_pi)
                    mapped.add(new_pi)
                    mapping[-1].append(new_pi)
                    to_consider.append(new_pi)
                    found_bond = True
                    break
            if not found_bond:
                to_consider.remove(pi)
    # sort it to be ascending in min atom index in molecule
    print('')
    for m in mapping:
        m.sort()
    mapping.sort(key=lambda x: min(x))
    return mapping

struct_dir = '/Users/sebastianjakymiw/Documents/Rochester/Work/run_scattering/'
N = 600
water = H2O()
water.name = 'Water'
water.label_rigid_bodies()
sys = mb.fill_box(compound=water, n_compounds = N, edge=0.25, density = 997.13)
sys.translate(-sys.pos)
boxd = sys.periodicity[0] #nm
box = mb.Box(mins=3 * [-boxd / 2], maxs=3 * [boxd / 2])

sys.save(struct_dir + 'water.gsd', overwrite=True)
print('box size is:{}'.format(box))

param_sys, kwargs = hoomd_ff.prepare_hoomd(sys, forcefield_files=['oplsaa.xml'], forcefield_debug=False, box=box)
mb.formats.gsdwriter.write_gsd(param_sys, struct_dir + 'water.gsd', shift_coords=True, **kwargs)

#Need to edit to add special pairs
g = gsd.hoomd.open(struct_dir + 'water.gsd')
frame = g[0]

c = hoomd.context.initialize('')
system = hoomd.init.read_gsd(filename = struct_dir + 'water.gsd')

nlist = hoomd.md.nlist.cell()
hoomd_ff.pair_coeffs(frame, param_sys, nlist)

#set-up bonds
hoomd_ff.bond_coeffs(frame, system, param_sys)

#set-up angles
hoomd_ff.angle_coeffs(frame, param_sys)

#set-up dihedrals
hoomd_ff.dihedral_coeffs(frame, param_sys)

#free particles from rigid bodies since rigid doesn't quite work for us
for i in range(frame.particles.N):
    system.particles[i].body = -1

#set-up groups
group_all = hoomd.group.all()

#time 1 = 48.9 fs
#emin
kT = 1.9872 / 1000
#fire = hoomd.md.integrate.mode_minimize_fire(dt=0.5 / 48.9, ftol=1e-4, Etol=1e-8)
#nve = hoomd.md.integrate.nve(group=group_all)
#init_dump = hoomd.dump.gsd(filename= struct_dir + 'init.gsd', period=1, group=group_all, phase=0, overwrite=True)

state_vars = ['temperature', 'volume', 'num_particles', 'pressure', 'lx', 'ly', 'lz']
log = hoomd.analyze.log(filename=None, quantities=state_vars, period=1)

# for i in range(1):
#     if not(fire.has_converged()):
#         print("fire not converged")
#         hoomd.run(100)

#bonds are constrained, so can use 2 ps
hoomd.md.integrate.mode_standard(dt=2 / 48.9)

#nve.disable()
#init_dump.disable()


#Now NVT
hoomd.md.integrate.mode_standard(dt=0.005)
nvt_dump = hoomd.dump.gsd(filename= struct_dir + 'trajectory.gsd', period=50, group=group_all, phase=0, overwrite=True)
npt = hoomd.md.integrate.npt(group=group_all, kT=298 * kT, tau=100 / 48.9, P=1.0, tauP=2.0)
npt.randomize_velocities(0)

#nl = hoomd.md.nlist.cell()
#lj = hoomd.md.pair.lj(r_cut=2.5, nlist=nl)
#lj.pair_coeff.set('A', 'A', epsilon=1.0, sigma=1.0)
# hoomd.md.integrate.mode_standard(dt=0.005)
# #hoomd.md.update.enforce2d()
# #nvt = hoomd.md.integrate.nvt(group=hoomd.group.all(), kT=0.15, tau=0.5)
# npt = hoomd.md.integrate.npt(group=group_all, kT=0.15, P=0.0, tau=0.5, tauP=2.0)
# npt.randomize_velocities(0)
sys = hoomd.data.system_data(hoomd.context.current.system_definition)
mol_indices = find_molecules(sys)

for m in mol_indices:
    print("molecule: " + str(m))

# print("mol_indices[0][0] " + str(mol_indices[0][0]))
# print("mol_indices[10] " + str(mol_indices[10]))


# print("len(mol_indices[0])" + str(len(mol_indices[0])))
# print("len(mol_indices[5])" + str(len(mol_indices[5])))
# print("len(mol_indices[1999])" + str(len(mol_indices[1999])))

def callback(sys):
    result = {k: log.query(k) for k in state_vars}
    result['density'] = result['num_particles'] / result['volume']
    return result

scale = 1
def set_callback(**data):
    if 'temperature' in data:
        print('temperature', data['temperature'])
        npt.set_params(kT = max(0.001,float(data['temperature'])))
    if 'pressure' in data:
        print('pressure', data['pressure'])
       # npt.set_params(P = float(data['pressure']))
    if 'box' in data:
        scale = float(data['box'])
        print('Resizing to', scale * log.query('lx') , ' x ', scale * log.query('lz'))
        if scale > 1:
            hoomd.update.box_resize(Lx=scale * log.query('lx') , Ly=scale * log.query('ly'), Lz=scale * log.query('lz'), period=None, scale_particles=False)
        else:    
            hoomd.update.box_resize(Lx=scale * log.query('lx') , Ly=scale * log.query('ly'), Lz=scale * log.query('lz'), period=None, scale_particles=True)
        
#For a 3d lattice, a message size of 400 (switched from 288) ended the allocation errors.
hoomd.hzmq.hzmq('tcp://*:5550', period=10, message_size=300, state_callback=callback, set_state_callback=set_callback)
c.sorter.disable()
for i in range(10000):
    hoomd.run(1e3)