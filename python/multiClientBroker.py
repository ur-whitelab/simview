import zmq
import sys
import time
from random import randint, random
import json

context = zmq.Context()

frontend = context.socket(zmq.ROUTER)#unity upstream
frontend.bind('tcp://*:5556')

publisher = context.socket(zmq.PUB)#unity downstream
publisher.bind('tcp://*:5559')

backend = context.socket(zmq.PAIR)#hoomd
backend.bind('tcp://*:5570')

poller = zmq.Poller()
poller.register(frontend, zmq.POLLIN)
poller.register(backend, zmq.POLLIN)

client_ids = []

expecting_state_update = False

default_state_update = {
    "temperature": "0.15",
    "pressure": "0",
    "box": "1"
}
default_state_update = json.dumps(default_state_update)
last_state_update_msg = ["state-update", default_state_update]

# all_bond_messages = []
# hoomd_initialized = False;
# unity_initialized = False;

def send_bonds_to_client(_id):
    print("sending bonds to client")
    for b_msg in all_bond_messages:
        msg = [_id, b_msg[0], b_msg[1]]
        frontend.send_multipart(msg)

    frontend.send_multipart([_id, "bonds-complete"])

while True:
    socks = dict(poller.poll())

    if socks.get(frontend) == zmq.POLLIN:
        message = frontend.recv_multipart()
        #First element is client id, 2nd is message type.
        client_id = message[0]
        msg_type = message[1]
        
        #The 'first-msg' and 'last-msg' if blocks are not strictly necessary but they provide useful debug info.
        if msg_type == 'first-msg':
            client_ids.append(client_id)
            print(str(client_id) + "is connected.")
            print(str(len(client_ids)) + " client(s) connected")
            #client initialized after hoomd so send it the bond data.
            # if hoomd_initialized:
            #     send_bonds_to_client(client_id)

            #unity_initialized = True

        elif msg_type == 'last-msg':
            client_ids.remove(client_id)
            print(str(client_id) + " is disconnected.")
            print(str(len(client_ids)) + " client(s) connected")
        elif msg_type == 'simulation-update':
            backend.send_multipart([msg_type, message[2]])
            expecting_state_update = False #Unity obliged Hoomd's state-update request

        #if this trips then Hoomd is expecting a state-update and Unity hasn't sent one
        if expecting_state_update:
            backend.send_multipart(last_state_update_msg)
            expecting_state_update = False

    if socks.get(backend) == zmq.POLLIN:
        message = backend.recv_multipart()
        msg_type = message[0]
        if msg_type == 'state-update':
            expecting_state_update = True
        # elif msg_type == 'bonds-update':
        #     all_bond_messages.append(message)
        #     print("num b messages: " + str(len(all_bond_messages)))

        publisher.send_multipart(message)
