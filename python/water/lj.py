import hoomd
import hoomd.md
import hoomd.hzmq
from mbuild.lib.moieties import H2O
import math, numpy as np, gsd, mbuild as mb, pickle
import hoomd_ff

struct_dir = '/Users/sebastianjakymiw/Documents/Rochester/Work/run_scattering/'
N = 1000
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

nlist = hoomd.md.nlist.cell(r_buff=0.001, check_period=1)
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
fire = hoomd.md.integrate.mode_minimize_fire(dt=0.5 / 48.9, ftol=1e-4, Etol=1e-8)
nve = hoomd.md.integrate.nve(group=group_all)
#init_dump = hoomd.dump.gsd(filename= struct_dir + 'init.gsd', period=1, group=group_all, phase=0, overwrite=True)

state_vars = ['temperature', 'volume', 'num_particles', 'pressure', 'lx', 'ly', 'lz']
log = hoomd.analyze.log(filename=None, quantities=state_vars, period=1)

for i in range(1):
    if not(fire.has_converged()):
        print("fire not converged")
        hoomd.run(100)

#bonds are constrained, so can use 2 ps
hoomd.md.integrate.mode_standard(dt=2 / 48.9)

nve.disable()
#init_dump.disable()


#Now NVT
hoomd.md.integrate.mode_standard(dt=0.005)
#nvt_dump = hoomd.dump.gsd(filename= struct_dir + 'trajectory.gsd', period=50, group=group_all, phase=0, overwrite=True)
nvt = hoomd.md.integrate.nvt(group=group_all, kT=298 * kT, tau=100 / 48.9)
nvt.randomize_velocities(0)

#nl = hoomd.md.nlist.cell()
#lj = hoomd.md.pair.lj(r_cut=2.5, nlist=nl)
#lj.pair_coeff.set('A', 'A', epsilon=1.0, sigma=1.0)
# hoomd.md.integrate.mode_standard(dt=0.005)
# #hoomd.md.update.enforce2d()
# #nvt = hoomd.md.integrate.nvt(group=hoomd.group.all(), kT=0.15, tau=0.5)
# npt = hoomd.md.integrate.npt(group=group_all, kT=0.15, P=0.0, tau=0.5, tauP=2.0)
# npt.randomize_velocities(0)

def callback(sys):
    result = {k: log.query(k) for k in state_vars}
    result['density'] = result['num_particles'] / result['volume']
    return result

scale = 1
def set_callback(**data):
    if 'temperature' in data:
        print('temperature', data['temperature'])
        #npt.set_params(kT = max(0.001,float(data['temperature'])))
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
        
#For a 3d lattice, a message size of 400 (switched from 288) ended the allocation errors.
hoomd.hzmq.hzmq('tcp://localhost:5570', period=50, message_size=375, state_callback=callback, set_state_callback=set_callback)
c.sorter.disable()
for i in range(10000):
    hoomd.run(1e3)
