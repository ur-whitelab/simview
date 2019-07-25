class UnityClient(object):
	def __init__(self, client_id, active_channel):
		self.initialized = False
		self.client_id = client_id
		self.active_channel = active_channel
		print(str(self.client_id) + " is connected.")

		