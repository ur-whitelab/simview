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
backend.connect('tcp://localhost:5570')

backend_spoke = context.socket(zmq.PAIR)#hoomd manager
backend_spoke.connect('tcp://localhost:5558')

poller = zmq.Poller()
poller.register(frontend, zmq.POLLIN)
poller.register(backend, zmq.POLLIN)

client_dict = {}


expecting_state_update = False

default_state_update = {
    "temperature": "0.15",
    "pressure": "0",
    "box": "1"
}
default_state_update = json.dumps(default_state_update)
last_state_update_msg = ["state-update", default_state_update]

all_bond_messages = []
all_particle_name_messages = []
hoomd_initialized = False

def reset_init_data():
    del all_particle_name_messages[:]
    del all_bond_messages[:]
    hoomd_initialized = False

def send_init_data_to_client(_id):
    for n_msg in all_particle_name_messages:
        msg = [_id, n_msg[0], n_msg[1]]
        frontend.send_multipart(msg)
    print("sent names to client " + _id)
    frontend.send_multipart([_id, "names-complete"])

    print("sending bonds to client " + _id)
    for b_msg in all_bond_messages:
        msg = [_id, b_msg[0], b_msg[1]]
        frontend.send_multipart(msg)

    frontend.send_multipart([_id, "bonds-complete"])

def send_init_data_to_clients():
    for _id in client_dict:
        if (client_dict[_id]):
            send_init_data_to_client(_id)

while True:
    socks = dict(poller.poll())

    if socks.get(frontend) == zmq.POLLIN:
        message = frontend.recv_multipart()
        #First element is client id, 2nd is message type.
        client_id = message[0]
        msg_type = message[1]
        
        if msg_type == 'first-msg':
            client_dict[client_id] = True #tell dict that this client is active.
            #client_ids.append(client_id)
            print(str(client_id) + "is connected.")
            print(str(len(client_dict)) + " client(s) connected")
            #client initialized after hoomd so send it the bond data.
            if hoomd_initialized:
                send_init_data_to_client(client_id)
        elif msg_type == 'last-msg':
            client_dict[client_id] = False
            #client_ids.remove(client_id)
            print(str(client_id) + " is disconnected.")
            print(str(len(client_dict)) + " client(s) connected")
        elif msg_type == 'simulation-update':
            backend.send_multipart([msg_type, message[2]])
            expecting_state_update = False #Unity obliged Hoomd's state-update request
        elif msg_type == 'hoomd-instructions':
            backend_spoke.send_multipart([msg_type, message[2]])

        #if this trips then Hoomd is expecting a state-update and Unity hasn't sent one
        if expecting_state_update:
            backend.send_multipart(last_state_update_msg)
            expecting_state_update = False

    if socks.get(backend) == zmq.POLLIN:
        message = backend.recv_multipart()
        msg_type = message[0]
        if msg_type == 'state-update':
            expecting_state_update = True
        elif msg_type == 'bonds-update':
            all_bond_messages.append(message)
        elif msg_type == 'bonds-complete':
            hoomd_initialized = True
            send_init_data_to_clients()
        elif msg_type == 'names-update':
            all_particle_name_messages.append(message)
        elif msg_type == 'hoomd-startup':
            reset_init_data()

        if msg_type == 'hoomd-startup':
            for _id in client_dict:
                if (client_dict[_id]):
                    msg = [_id, msg_type]
                    frontend.send_multipart(msg)
        else:
             publisher.send_multipart(message)

                    
        
