
class SimulationChannel(object):

	particle_name_messages = []
	bond_messages = []
	initialized = False

	def __init__(self, context, ip_address, socket, simulation_type):
		self.simulation_type = simulation_type
		self.ip_address = ip_address
		self.socket = socket
		print("New simulation channel for " + str(simulation_type) + " connected to " + str(ip_address))

	def reset_init_data(self):
		del self.particle_name_messages[:]
		del self.bond_messages[:]
		self.initialized = False

		