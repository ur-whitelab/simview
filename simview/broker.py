import zmq
import time
import fire
import json
from .simulation import SimulationChannel

def broker(sim_type_list=[], sim_ports_list=[], debug=False):
    print(str(len(sim_type_list)) + ' simulations requested from the command line')
    if not len(sim_type_list) == len(sim_ports_list):
        raise ValueError('Arguments sim_type_list and sim_ports_list must '
                         'be of equal length, but got {} (sim_type_list) '
                         'and {} (sim_ports_list)'.format(
                             len(sim_type_list),
                             len(sim_ports_list)))

    base_ip_address = 'tcp://localhost:'
    initialized_simulations = 0
    default_port_val = '8080'

    if (len(sim_type_list)==0):
        print('No simulations requested! Defaulting to one type only at port {}.'.format(default_port_val))
        sim_type_list = ['A']
        sim_ports_list = [default_port_val]

    active_channel = 0
    active_channel_changed = False
    channels = []
    frames_since_last_update_per_channel = []
    frame_count = 0
    num_framecompletes_sent_to_clients = 0

    context = zmq.Context()

    # these ports are fixed to match with Unity
    publisher = context.socket(zmq.PUB)# unity downstream
    publisher.bind('tcp://*:5572')

    initialization_publisher = context.socket(zmq.PUB)
    initialization_publisher.bind('tcp://*:5573')

    active_channel_publisher = context.socket(zmq.PUB)
    active_channel_publisher.bind('tcp://*:5574')

    instructor_pipe = context.socket(zmq.PAIR)
    instructor_pipe.bind('tcp://*:5575')
    # end Unity ports

    latest_state_update = {
        'temperature': '77',
        'pressure': '1',
        'density': '{}'.format(700),
        'box': '1'
    }

    # generate dict of ports keyed by type
    sim_ports = dict(zip(sim_type_list, sim_ports_list))

    poller = zmq.Poller()

    instructor_poller =  zmq.Poller()
    instructor_poller.register(instructor_pipe, zmq.POLLIN)

    if debug:
        print('registering poller')

    for i in range(0, len(sim_type_list)):
        print('starting simulation channel ' + str(i) + ' of type ' + str(sim_type_list[i]))

        sim_ip = base_ip_address + sim_ports[sim_type_list[i]]

        sc_socket = context.socket(zmq.PAIR)
        sc_socket.connect(sim_ip)
        poller.register(sc_socket, zmq.POLLIN)

        sc = SimulationChannel(context, sim_ip, sc_socket, str(i))
        channels.append(sc)

        frames_since_last_update_per_channel.append(0)

    for i in range (0, len(channels)):
        if (i == active_channel):
            print('active channel is ' + str(i))
        print(str(i) + ' channel is a sim of type ' + str(channels[i].simulation_type))
        print('it has an ip address of ' + str(channels[i].ip_address))

    waitingToSend = False
    start_time = time.time()

    def send_channel_data_to_all_clients():
        active_channel_string = str(active_channel)
        active_channel_message = bytes(active_channel_string, 'utf-8')

        active_channel_publisher.send_multipart([b'channel-update', active_channel_message])

    def send_init_data_to_all_clients():

        str_ac = str(active_channel)
        channel_aware_message = bytes(str_ac, 'utf-8')

        initialization_publisher.send_multipart([b're-init', channel_aware_message])
        print("sent re-init message on frame " + str(frame_count))

        pnames_data = channels[active_channel].particle_name_messages
        b_data = channels[active_channel].bond_messages

        print('sending ' + str(len(pnames_data)) + ' particle name messages and ' + str(len(b_data)) + ' bond messages to unity')
        print('...from channel ' + str(active_channel) + ' of type ' + str(channels[active_channel].simulation_type))

        for n_msg in pnames_data:
            msg = [n_msg[0], n_msg[1]]
            initialization_publisher.send_multipart(msg)

        print('sent names to clients.')
        initialization_publisher.send_multipart([b'names-complete', channel_aware_message])

        print('sending bonds to clients.')
        for b_msg in b_data:
            msg = [b_msg[0], b_msg[1]]
            initialization_publisher.send_multipart(msg)

        initialization_publisher.send_multipart([b'bonds-complete', channel_aware_message])

    while True:
        # check the pipe to the instructor client to see if there's data in there.
        # pollers don't block the thread

        instr_poll = dict(instructor_poller.poll(10))
        if instr_poll.get(instructor_pipe) == zmq.POLLIN:
            print("polling")
            instr_msg = instructor_pipe.recv_multipart()
            instr_msg_type = instr_msg[0]
            if instr_msg_type == b'sim-update':
                latest_state_update = instr_msg[1]
                latest_state_update = json.loads(latest_state_update)
                print('This is from unity:{}'.format(latest_state_update))
                # channels[active_channel].socket.send(latest_state_update, flags=zmq.NOBLOCK)
            if instr_msg_type == b'channel-change':
                print('channel switched from ' + str(active_channel) + ' to ' + str(instr_msg[1]))
                active_channel = int(instr_msg[1])

        # end of instructor pipe check code.

        socks = dict(poller.poll(10))

        if not waitingToSend:
            start_time = time.time()
            waitingToSend = True

        for i in range(0, len(channels)):
            channel_socket = channels[i].socket
            if socks.get(channel_socket) == zmq.POLLIN:

                if (frame_count % 1000 == 0):
                    print('Update from channel ' + str(i) + '; there have been ' + str(frames_since_last_update_per_channel[i]) + ' frames since the last update')

                frames_since_last_update_per_channel[i] = 0
                message = channel_socket.recv_multipart()
                msg_type = message[0]
                # handle various initialization method types
                if msg_type == b'names-update':
                    channels[i].particle_name_messages.append(message)
                elif msg_type == b'bonds-update':
                    channels[i].bond_messages.append(message)
                elif msg_type == b'bonds-complete':
                    channels[i].initialized = True
                    initialized_simulations += 1
                    print('channel of type ' + str(channels[i].simulation_type) + ' is initialized')
                    print('num of name messages in channel ' + str(i) + ' of type ' + str(channels[i].simulation_type) + ': ' + str(len(channels[i].particle_name_messages)))
                    print('num of bond messages in channel ' + str(i) + ' of type ' + str(channels[i].simulation_type) + ': ' + str(len(channels[i].bond_messages)))
                    print('number of initialized simulations: ' + str(initialized_simulations))
                elif msg_type == b'state-update':
                    if type(latest_state_update) is str:
                        latest_state_update = json.loads(latest_state_update)
                    latest_state_update = json.dumps(latest_state_update)
                    response_state_msg_update = [b'state-update', bytes(latest_state_update, encoding='utf-8')]
                    channels[i].socket.send_multipart(response_state_msg_update)
                    print('sent response state msg update')
                # should execute only for the activate simulation and only once the above init code has run.
                elif (i == active_channel and channels[i].initialized):
                    fps = 1.0 / max((time.time() - start_time), 0.0001)

                    waitingToSend = False
                    str_ac = str(active_channel)
                    channel_aware_message = [bytes(str_ac, 'utf-8')]
                    if debug:
                        print(str(message) + "\n")
                    for m in message:
                        channel_aware_message.append(m)
                        if debug:
                            print('cam: ' + str(channel_aware_message))
                    if msg_type == b'frame-complete':
                        if (frame_count % 1000 == 0):
                            print('fps that publisher sent frame-complete: ' + str(fps))
                            print('number of frame-completes sent to clients: ' + str(num_framecompletes_sent_to_clients) + ' at frame ' + str(frame_count) )
                        num_framecompletes_sent_to_clients += 1
                    # only send message from publisher if its from the active channel.
                    publisher.send_multipart(channel_aware_message)

            else:
                frames_since_last_update_per_channel[i] += 1
            # end socket polling

        if active_channel_changed:
            send_channel_data_to_all_clients()
            active_channel_changed = False

        if frame_count % 1000 == 0:
            send_init_data_to_all_clients()

        frame_count += 1


def main():
	fire.Fire(broker)

if __name__ == "__main__":
    main()
