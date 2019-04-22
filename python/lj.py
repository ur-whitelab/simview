import hoomd
import hoomd.md
import hoomd.hzmq


c = hoomd.context.initialize()
system = hoomd.init.create_lattice(unitcell=hoomd.lattice.sq(a=4.0), n=[180, 320])
nl = hoomd.md.nlist.cell();
lj = hoomd.md.pair.lj(r_cut=2.5, nlist=nl);
lj.pair_coeff.set('A', 'A', epsilon=1.0, sigma=1.0);
hoomd.md.integrate.mode_standard(dt=0.001)
hoomd.md.update.enforce2d()
hoomd.md.integrate.nvt(group=hoomd.group.all(), kT=0.15, tau=10 ).randomize_velocities(seed=1)
hoomd.hzmq.hzmq('tcp://*:5000', period=100)
c.sorter.disable()
hoomd.run(1e7)
