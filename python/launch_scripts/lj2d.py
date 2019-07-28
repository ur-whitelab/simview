import hoomd
import hoomd.md
import hoomd.hzmq

c = hoomd.context.initialize()
system = hoomd.init.create_lattice(unitcell=hoomd.lattice.sq(a=4.0, type_name='A'), n=[36,64])
#system = hoomd.init.create_lattice(unitcell=hoomd.lattice.sq(a=4.0), n=[72, 128])

nl = hoomd.md.nlist.cell()
lj = hoomd.md.pair.lj(r_cut=2.5, nlist=nl)
lj.pair_coeff.set('A', 'A', epsilon=2.0, sigma=1.0)
#lj.pair_coeff.set('A', 'B', epsilon=2.0, sigma=1.0, alpha=0.5, r_cut=2.5, r_on=2.0);
hoomd.md.integrate.mode_standard(dt=0.005)
hoomd.md.update.enforce2d()
nvt = hoomd.md.integrate.nvt(group=hoomd.group.all(), kT=0.15, tau=0.5)
#npt = hoomd.md.integrate.npt(group=hoomd.group.all(), kT=0.15, P=0.0, tau=0.5, tauP=2.0)
nvt.randomize_velocities(0)
state_vars = ['temperature', 'volume', 'num_particles', 'pressure', 'lx', 'ly', 'lz']
log = hoomd.analyze.log(filename=None, quantities=state_vars, period=1)

def callback(sys):
    result = {k: log.query(k) for k in state_vars}
    result['density'] = result['num_particles'] / result['volume']
    return result

scale = 1
def set_callback(**data):
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
        
#For a 3d lattice, a message size of 400 (switched from 288) ended the allocation errors.
hoomd.hzmq.hzmq('tcp://*:5552', period=25, message_size=288, state_callback=callback, set_state_callback=set_callback)
c.sorter.disable()
for i in range(10000):
    hoomd.run(1e3)