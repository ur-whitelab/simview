from hoomd.zmq import _zmq, hoomd
import zmq

class tfcompute(hoomd.compute._compute):
    def __init__(self, uri):
        self.ctx = zmq.Context()
        hoomd.context.msg.notice(2, 'Opening PUB Socket on {}.\n'.format(ur))
        self.sock = self.ctx.socket(zmq.PUB)
        self.sock.connect(uri)
