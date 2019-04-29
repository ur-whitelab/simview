from hoomd.hzmq import _hzmq
import hoomd

class hzmq:
    def __init__(self, uri, period=100, message_size=340):
        #340 -> gives about 1400 byte packet which is recommended

        if not hoomd.init.is_initialized():
            raise RuntimeError('Must create ZMQ after hoomd initialization')

        self.cpp_hook = _hzmq.ZMQHook(hoomd.context.current.system_definition, period, uri, message_size)

        integrator = hoomd.context.current.integrator
        if integrator is None:
            raise ValueError('Must have integrator set to receive forces')
        self.enabled = False
        self.enable()

    def enable(self):
        if not self.enabled:
            hoomd.context.current.integrator.cpp_integrator.setHalfStepHook(self.cpp_hook)
            self.enabled = True

    def disable(self):
        if self.enabled:
            hoomd.context.current.integrator.cpp_integrator.removeHalfStepHook(self.cpp_hook)
            self.enabled = False
