import hoomd
import hoomd.md
import hoomd.hzmq

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

c = hoomd.context.initialize()
system = hoomd.init.create_lattice(unitcell=hoomd.lattice.bcc(a=4.0, type_name='A'), n=10)
#system = hoomd.init.create_lattice(unitcell=hoomd.lattice.sq(a=4.0), n=[72, 128])

nl = hoomd.md.nlist.cell()
lj = hoomd.md.pair.lj(r_cut=2.5, nlist=nl)
lj.pair_coeff.set('A', 'A', epsilon=2.0, sigma=1.0)
#lj.pair_coeff.set('A', 'B', epsilon=2.0, sigma=1.0, alpha=0.5, r_cut=2.5, r_on=2.0);
hoomd.md.integrate.mode_standard(dt=0.005)
#hoomd.md.update.enforce2d()
nvt = hoomd.md.integrate.nvt(group=hoomd.group.all(), kT=0.15, tau=0.5)
#npt = hoomd.md.integrate.npt(group=hoomd.group.all(), kT=0.15, P=0.0, tau=0.5, tauP=2.0)
nvt.randomize_velocities(0)
state_vars = ['temperature', 'volume', 'num_particles', 'pressure', 'lx', 'ly', 'lz']
log = hoomd.analyze.log(filename=None, quantities=state_vars, period=1)

sys = hoomd.data.system_data(hoomd.context.current.system_definition)
mol_indices = find_molecules(sys)

print("mol_indices[0][0] " + str(mol_indices[0][0]))
print("mol_indices[10] " + str(mol_indices[10]))


print("len(mol_indices[0])" + str(len(mol_indices[0])))
print("len(mol_indices[5])" + str(len(mol_indices[5])))
print("len(mol_indices[1999])" + str(len(mol_indices[1999])))


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
hoomd.hzmq.hzmq('tcp://*:5551', period=50, message_size=400, state_callback=callback, set_state_callback=set_callback)
c.sorter.disable()
for i in range(10000):
    hoomd.run(1e3)
