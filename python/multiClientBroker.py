import zmq
import sys
import time
from random import randint, random
import json

from Simulation import SimulationChannel
from UnityClient import UnityClient


print(str(len(sys.argv) - 1) + " simulations requested from the command line")

base_ip_address = "tcp://localhost:"

sim_type_list = []

if (len(sys.argv)== 1):
    sim_type_list = ["h2o"]
else:
    sim_type_list = sys.argv[1:]

active_channel = 1
channels = []

context = zmq.Context()

frontend = context.socket(zmq.ROUTER)#unity upstream
frontend.bind('tcp://*:5556')

publisher = context.socket(zmq.PUB)#unity downstream
publisher.bind('tcp://*:5559')

# backend = context.socket(zmq.PAIR)#hoomd
# backend.connect('tcp://localhost:5570')

poller = zmq.Poller()
poller.register(frontend, zmq.POLLIN)
#poller.register(backend, zmq.POLLIN)

sim_ports = {
    "h2o": "5550",
    "lj": "5551"
}

for i in range(0, len(sim_type_list)):
    print("starting simulation channel " + str(i) + " of type " + str(sim_type_list[i]))

    sim_ip = base_ip_address + sim_ports[sim_type_list[i]]

    sc_socket = context.socket(zmq.PAIR)
    sc_socket.connect(sim_ip)
    poller.register(sc_socket, zmq.POLLIN)

    sc = SimulationChannel(context, sim_ip, sc_socket, sim_type_list[i])
    channels.append(sc)

#backend = channels[active_channel].socket

for i in range (0, len(channels)):
    if (i == active_channel):
        print("active channel is " + str(i))
    print("i: " + str(i) + " channel is a sim of type " + str(channels[i].simulation_type))
    print("it has an ip address of " + str(channels[i].ip_address))

client_dict = {}

expecting_state_update = False

default_state_update = {
    "temperature": "0.15",
    "pressure": "0",
    "box": "1"
}
default_state_update = json.dumps(default_state_update)
last_state_update_msg = ["state-update", default_state_update]

#all_bond_messages = []
#all_particle_name_messages = []
#hoomd_initialized = False

# def reset_init_data():
#     del all_particle_name_messages[:]
#     del all_bond_messages[:]
#     hoomd_initialized = False

def send_init_data_to_client(_id):

    pnames_data = channels[active_channel].particle_name_messages
    b_data = channels[active_channel].bond_messages

    print("sending " + str(len(pnames_data)) + " particle names and " + str(len(b_data)) + " bonds to unity")
    print("...from channel " + str(active_channel) + " of type " + str(channels[active_channel].simulation_type))

    for n_msg in pnames_data:
        msg = [_id, n_msg[0], n_msg[1]]
        frontend.send_multipart(msg)
    print("sent names to client " + _id)
    frontend.send_multipart([_id, "names-complete"])

    print("sending bonds to client " + _id)
    for b_msg in b_data:
        msg = [_id, b_msg[0], b_msg[1]]
        frontend.send_multipart(msg)

    client_dict[_id].active_channel = active_channel

    frontend.send_multipart([_id, "bonds-complete"])

def send_init_data_to_all_clients():
    for _id in client_dict:
        send_init_data_to_client(_id)

initialized_simulations = 0

while True:
    socks = dict(poller.poll())

    for i in range(0, len(channels)):
        channel_socket = channels[i].socket
        if socks.get(channel_socket) == zmq.POLLIN:
            message = channel_socket.recv_multipart()
            msg_type = message[0]
            print(str(channels[i].simulation_type) + " --- " + str(msg_type))
            if msg_type == 'names-update':
                channels[i].particle_name_messages.append(message)
            elif msg_type == 'bonds-update':
                channels[i].bond_messages.append(message)
            elif msg_type == 'bonds-complete':
                channels[i].initialized = True
                initialized_simulations += 1
                print("channel of type " + str(channels[i].simulation_type) + " is initialized")  
                print("num of pnames in channel " + str(i) + " of type " + str(channels[i].simulation_type) + ": " + str(len(channels[i].particle_name_messages)))
                print("number of initialized simulations: " + str(initialized_simulations))
            elif msg_type == 'hoomd-startup':
                channels[i].reset_init_data()
                initialized_simulations -= 1
                #we only need to worry about unity-hoomd communication with the active channel
            elif (i == active_channel and channels[i].initialized):
                if msg_type == 'state-update':
                    expecting_state_update = True   
                publisher.send_multipart(message)

    #check that each client is aware of the current active channel
    for client_id in client_dict:
        uc = client_dict[client_id]
        if (uc.active_channel != active_channel or not uc.initialized) and channels[active_channel].initialized:
            print("client " + str(uc.client_id) + " thinks that the active_channel is " + str(uc.active_channel))
            send_init_data_to_client(uc.client_id)

    if socks.get(frontend) == zmq.POLLIN:
        message = frontend.recv_multipart()
        #First element is client id, 2nd is message type.
        client_id = message[0]
        msg_type = message[1]
        
        if msg_type == 'first-msg':
            uc = UnityClient(client_id, active_channel)
            client_dict[client_id] = uc
            if (channels[active_channel].initialized):
                print("ac init")
                send_init_data_to_client(client_id)
                client_dict[client_id].initialized = True
            else:
                print("ac not init")
                client_dict[client_id].initialized = False
            
            print(str(len(client_dict)) + " client(s) connected")

        elif msg_type == 'last-msg':
            del client_dict[client_id]
            print(str(client_id) + " is disconnected.")
            print(str(len(client_dict)) + " client(s) connected")

        elif msg_type == 'simulation-update':
            channels[active_channel].socket.send_multipart([msg_type, message[2]])
            #backend.send_multipart([msg_type, message[2]])
            expecting_state_update = False #Unity obliged Hoomd's state-update request
        #if this trips then Hoomd is expecting a state-update and Unity hasn't sent one
        if expecting_state_update:
            channels[active_channel].socket.send_multipart(default_state_update)
            #backend.send_multipart(default_state_update)
            expecting_state_update = False





                    
        