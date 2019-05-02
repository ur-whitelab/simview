import hoomd
import hoomd.md
import hoomd.hzmq


c = hoomd.context.initialize()
system = hoomd.init.create_lattice(unitcell=hoomd.lattice.sq(a=4.0), n=[18, 32])
nl = hoomd.md.nlist.cell()
lj = hoomd.md.pair.lj(r_cut=2.5, nlist=nl)
lj.pair_coeff.set('A', 'A', epsilon=1.0, sigma=1.0)
hoomd.md.integrate.mode_standard(dt=0.001)
hoomd.md.update.enforce2d()
#npt = hoomd.md.integrate.npt(group=hoomd.group.all(), kT=0.15, P=0, tau=0.5, tauP=2)
npt = hoomd.md.integrate.nvt(group=hoomd.group.all(), kT=0.15, tau=0.5)
npt.randomize_velocities(0)
state_vars = ['temperature', 'volume', 'num_particles', 'pressure', 'lx', 'ly', 'lz']
log = hoomd.analyze.log(filename=None, quantities=state_vars, period=1)

def callback(sys):
    result = {k: log.query(k) for k in state_vars}
    result['density'] = result['num_particles'] / result['volume']
    return result


def set_callback(**data):
    if 'temperature' in data:
        print('temperature', data['temperature'])
        npt.set_params(kT = float(data['temperature']))
    if 'pressure' in data:
        print('pressure', data['pressure'])
        npt.set_params(P = float(data['pressure']))

hoomd.hzmq.hzmq('tcp://*:5000', period=100, message_size=340,state_callback=callback, set_state_callback=set_callback)
c.sorter.disable()
for i in range(100):
    hoomd.run(101)
