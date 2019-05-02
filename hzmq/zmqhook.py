from hoomd.hzmq import _hzmq
import hoomd, json

class hzmq:
    def __init__(self, uri, period=100, message_size=340, state_callback=None, set_state_callback = None):
        #340 -> gives about 1400 byte packet which is recommended

        if not hoomd.init.is_initialized():
            raise RuntimeError('Must create ZMQ after hoomd initialization')

        self.cpp_hook = _hzmq.ZMQHook(self, hoomd.context.current.system_definition, period, uri, message_size)

        integrator = hoomd.context.current.integrator
        if integrator is None:
            raise ValueError('Must have integrator set to receive forces')
        self.enabled = False
        self.enable()
        self.state_callback = state_callback
        if state_callback is None:
            self.state_callback = lambda x: {}
        self.set_state_callback = set_state_callback
        if set_state_callback is None:
            self.set_state_callback = lambda **x: ''

    def get_state_msg(self):
        return json.dumps(self.state_callback(hoomd.context.current.system_definition))

    def set_state_msg(self, msg):
        try:
            data = json.loads(msg)
        except json.decoder.JSONDecodeError:
            print('Mangled message', msg)
            return
        self.set_state_callback(**data)

    def enable(self):
        if not self.enabled:
            hoomd.context.current.integrator.cpp_integrator.setHalfStepHook(self.cpp_hook)
            self.enabled = True

    def disable(self):
        if self.enabled:
            hoomd.context.current.integrator.cpp_integrator.removeHalfStepHook(self.cpp_hook)
            self.enabled = False
