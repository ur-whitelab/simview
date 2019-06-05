import zmq, time, json
import HZMsg.Frame as frame

'''This is an example client file for testing receiving encoded messages containing the 
output from an ongoing HOOMD-blue simulation. See lj.py and server.py for how this is 
constructed and encoded.'''

context = zmq.Context()
sock = context.socket(zmq.PAIR)
#sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:5000')

count = 0
while True:
    t = time.time()
    mtime = None
    while True:
        msg = sock.recv_multipart()
        mtype = msg[0].decode()
        if mtype == 'frame-complete':
            count += 1
            break
        buf = bytearray(msg[1])
        f = frame.Frame.GetRootAsFrame(buf, 0)
        print(f.N(), f.I())
    print('count',count)
    if count % 10 == 0:
        print((time.time() - t) * f.N() / count, ' ms', count, 'parts')
        msg = sock.recv_multipart()
        mtype = msg[0].decode()
        assert mtype == 'state-update', 'got ' + mtype + ' instead of state-update'
        state_msg = msg[1].decode()
        state = json.loads(state_msg)
        print(state)
        sock.send_multipart(['state-set'.encode(), json.dumps({'box': 1.001}).encode()])
        count = 0


