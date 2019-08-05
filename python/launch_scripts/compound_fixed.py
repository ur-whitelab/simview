import hoomd
import hoomd.md
import hoomd.hzmq
from mbuild.lib.moieties import H2O, CH3
import math, numpy as np, gsd, mbuild as mb, pickle
import hoomd_ff
import fire

class Ethane(mb.Compound):
    """An ethane molecule. """
    def __init__(self):
        """Connect two methyl groups to form an ethane. """
        super(Ethane, self).__init__()
        self.add(CH3(), 'methyl1')
        self.add(CH3(), 'methyl2')
        mb.force_overlap(self['methyl1'], self['methyl1']['up'], self['methyl2']['up'])
        self.name = 'Ethane'

class Methane(mb.Compound):
    def __init__(self):
        super(Methane, self).__init__()
        carbon = mb.Particle(name='C')
        self.add(carbon)

        hydrogen = mb.Particle(name='H', pos=[0.1, 0, -0.07])
        self.add(hydrogen)

        self.add_bond((self[0], self[1]))

        self.add(mb.Particle(name='H', pos=[-0.1, 0, -0.07]))
        self.add(mb.Particle(name='H', pos=[0, 0.1, 0.07]))
        self.add(mb.Particle(name='H', pos=[0, -0.1, 0.07]))

        self.add_bond((self[0], self[2]))
        self.add_bond((self[0], self[3]))
        self.add_bond((self[0], self[4]))
        self.name = 'Methane'

class Methanol(mb.Compound):
    def __init__(self):
        super(Methanol, self).__init__()
        mb.load('methanol.pdb', compound=self)
        self.translate(-self[0].pos)
        self.name = 'Methanol'

class Propanol(mb.Compound):
    def __init__(self):
        super(Propanol, self).__init__()
        mb.load('propanol.pdb', compound=self)
        self.translate(-self[0].pos)
        self.name = 'Propanol'

class MethylCyanide(mb.Compound):
    def __init__(self):
        super(MethylCyanide, self).__init__()
        mb.load('methyl_cyanide.pdb', compound=self)
        self.translate(-self[0].pos)
        self.name = 'MethylCyanide'

class Methylamine(mb.Compound):
    def __init__(self):
        super(Methylamine, self).__init__()
        mb.load('methylamine.pdb', compound=self)
        self.translate(-self[0].pos)
        self.name = 'Methylamine'

def run(compound, socket=None, period = 1, temperature = 77, pressure = 1, density = None, epsilon = 1.0, sigma = 1.0, particle_number=10000, steps=1e6, aspect_ratio=16 / 9, filename = None):

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

    compound = compound.lower()
    if compound == 'h2o':
        if density == None:
            density = 997.13
        sys = mb.fill_box(compound = H2O(), n_compounds = particle_number, edge = 0.25, density = density)
    elif compound == 'methanol':
        if density == None:
            density = 792
        sys = mb.fill_box(compound = Methanol(), n_compounds = particle_number, edge = 0.25, density = density)
    elif compound == 'ethane':
        if density == None:
            density = 336.07
        sys = mb.fill_box(compound = Ethane(), n_compounds = particle_number, edge = 0.25, density = density)
    elif compound == 'methylcyanide':
        if density == None:
            density = 786
        sys = mb.fill_box(compound = MethylCyanide(), n_compounds = particle_number, edge = 0.25, density = density)
    elif compound == 'propanol':
        if density == None:
            density = 805.3
        sys = mb.fill_box(compound = Propanol(), n_compounds = particle_number, edge = 0.25, density = density)
    elif compound == 'methylamine':
        if density == None:
            density = 662.4
        sys = mb.fill_box(compound = Methylamine(), n_compounds = particle_number, edge = 0.25, density = density)

    sys.translate(-sys.pos)
    boxd = sys.periodicity[0] #nm
    box = mb.Box(mins=3 * [-boxd / 2], maxs=3 * [boxd / 2])
    sys.save(compound + '.gsd', overwrite=True)
    print('box size is:{}'.format(box))

    param_sys, kwargs = hoomd_ff.prepare_hoomd(sys, forcefield_files=['oplsaa.xml'], forcefield_debug=False, box=box)
    mb.formats.gsdwriter.write_gsd(param_sys, compound + '.gsd', shift_coords=True, **kwargs)

    #Need to edit to add special pairs
    g = gsd.hoomd.open(compound + '.gsd')
    frame = g[0]

    c = hoomd.context.initialize('')
    system = hoomd.init.read_gsd(filename = compound + '.gsd')

    nlist = hoomd.md.nlist.cell(r_buff=0.001, check_period=1)
    hoomd_ff.pair_coeffs(frame, param_sys, nlist, r_cut = 4.0)

    #set-up pppm
    charged = hoomd.group.all();
    pppm = hoomd.md.charge.pppm(nlist=nlist, group=charged)
    pppm.set_params(Nx=32, Ny=32, Nz=32, order=6, rcut=4.0)

    #set-up bonds
    hoomd_ff.bond_coeffs(frame, system, param_sys)

    #set-up angles
    hoomd_ff.angle_coeffs(frame, param_sys)

    #set-up dihedrals
    hoomd_ff.dihedral_coeffs(frame, param_sys)

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
    nvt_dump = hoomd.dump.gsd(filename= compound +  '_trajectory.gsd', period=1, group=group_all, phase=0, overwrite=True)
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


