import sys
import zmq, time, json
import HZMsg.Frame as frame
import pickle

port = int(sys.argv[1])

packet_save_file_path = 'playback_frames.p'
frames_dict = {}

context = zmq.Context()
sock = context.socket(zmq.PAIR)
#sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:' + str(port))

count = 0

bonds_update_list = []
names_update_list = []
state_update_dict = {}

#read init data:
while True:
    msg = sock.recv_multipart()
    mtype = msg[0].decode()
    print('mtype: ' + str(mtype))
    if mtype == 'names-update':
        names_update_list.append(msg[1])
        print('appended following names message: ' + str(msg[1]))
    elif mtype == 'names-complete':
        frames_dict['names'] = names_update_list
        print('number of names messages after names-complete: ' + str(len(names_update_list)))
    elif mtype == 'bonds-update':
        bonds_update_list.append(msg[1])
        print('appended following bond message: ' + str(msg[1]))
    elif mtype == 'bonds-complete':
        frames_dict['bonds'] = bonds_update_list
        print("number of bond messages is after bonds-complete: " + str(len(bonds_update_list)))
        #Init phase is over after bonds-complete is sent.
        break
packet_idx = 0
while True:
    t = time.time()
    mtime = None
    update_positions_buf = []
    while True:
        msg = sock.recv_multipart()
        mtype = msg[0].decode()
        if mtype == 'frame-complete':
            packet_idx += 1
            frames_dict['num_frames'] = packet_idx
            print("packet_idx: " + str(packet_idx))
            count += 1
            break
        if mtype != 'frame-update':
            print('incorrect message type: ' + str(mtype))
            break

        buf = bytearray(msg[1])
        update_positions_buf.append(buf)
        frames_dict[str(packet_idx)] = update_positions_buf

        pickle.dump(frames_dict, open(packet_save_file_path, 'wb'))
        
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
        state_update_dict[str(packet_idx)] = state_msg
        frames_dict['state-updates'] = state_update_dict
        print(state)
        sock.send_multipart(['state-set'.encode(), json.dumps({'box': 1.001}).encode()])
        count = 0

    



