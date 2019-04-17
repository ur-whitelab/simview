import zmq

context = zmq.Context()
sock = context.socket(zmq.SUB)
sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:5000')
while True:
    msg = sock.recv()
    print(msg)