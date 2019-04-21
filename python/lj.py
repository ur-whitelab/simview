import hoomd
import hoomd.md
import hoomd.zmq


hoomd.context.initialize()
system = hoomd.init.create_lattice(unitcell=hoomd.lattice.sq(a=4.0), n=[6,6])
hoomd.md.integrate.mode_standard(dt=0.005)
hoomd.md.integrate.nve(group=hoomd.group.all()).randomize_velocities(kT=2, seed=2)
hoomd.zmq.hzmq('tcp://*:5000')
hoomd.run(1e7)
