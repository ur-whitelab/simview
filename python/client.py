import zmq, time
import HZMsg.Frame as frame

context = zmq.Context()
sock = context.socket(zmq.SUB)
sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:8888')

while True:
    t = time.time()
    for i in range(1000):
        msg = sock.recv_multipart()
    msg = sock.recv_multipart()[1]
    buf = bytearray(msg)
    f = frame.Frame.GetRootAsFrame(buf, 0)
    print((time.time() - t) * f.N(), 'ms', f.N(), f.I())
