﻿using System.Collections;
using System.Collections.Generic;
using FlatBuffers;
using HZMsg;
using UnityEngine;
using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;

public class FilterChannelClient : MonoBehaviour
{
    //public string BROKER_IP_ADDRESS = "tcp://ar-table.che.rochester.edu:";
    public string BROKER_IP_ADDRESS = "tcp://10.5.12.72:";

    public delegate void NewFrameAction(Frame frame);
    public delegate void CompleteFrameAction();
    public delegate void SimulationUpdateAction(Dictionary<string, string> state);
    public event NewFrameAction OnNewFrame;
    public event CompleteFrameAction OnCompleteFrame;
    //public event NewFrameAction OnNewFrame_Forced;
    //public event CompleteFrameAction OnCompleteFrame_Forced;
    public event SimulationUpdateAction OnSimulationUpdate;

    public event NewBondFrameAction OnNewBondFrame;
    public event CompleteBondFrameAction OnCompleteBondFrame;
    public delegate void NewBondFrameAction(string msg_string);
    public delegate void CompleteBondFrameAction();

    public event NewParticleNameAction OnNewName;
    public event CompleteParicleNameAction OnCompleteNames;
    public delegate void NewParticleNameAction(string msg_string);
    public delegate void CompleteParicleNameAction();

    public event HoomdStartupAction OnHoomdStartup;
    public delegate void HoomdStartupAction();

    private SubscriberSocket subscriberSocket;
    private SubscriberSocket initializationSocket;
    private SubscriberSocket activeChannelSocket;
    //NO BLOCK
    private System.TimeSpan waitTime = new System.TimeSpan(0, 0, 0);

    int frames_since_last_msg_from_publisher = 0;

    private bool all_bonds_read = false;

    private bool reinitializing = false;
    private bool initialized_on_correct_channel = false;
    private bool init_socket_connected = false;

    int local_active_simulation = -1;
    int activeSimFromZMQ = 0;
    int updates = 0;
    int num_framecompletes_from_broker = 0;
    int frame_count = 0;
    // Start is called before the first frame update
    void Start()
    {
        ZMQStartUp();
    }

    private bool QueryInitializationPublisher()
    {
        if (!init_socket_connected)
        {
            ConnectToInitSocket();
        }

        List<byte[]> message = null;

        bool recieved = initializationSocket.TryReceiveMultipartBytes(waitTime, ref message, 2);

        if (!recieved || message == null)
        {
            return false;
        }

        string messageType = System.Text.Encoding.UTF8.GetString(message[0]);
        // Debug.Log("init msg type: " + messageType);
        switch (messageType)
        {
            case ("bonds-update"):
                if (OnNewBondFrame != null)
                    OnNewBondFrame(System.Text.Encoding.UTF8.GetString(message[1]));
                break;

            case ("bonds-complete"):
                if (OnCompleteBondFrame != null)
                    OnCompleteBondFrame();
                return true;

            case ("names-update"):
                if (OnNewName != null)
                    OnNewName(System.Text.Encoding.UTF8.GetString(message[1]));
                break;

            case ("names-complete"):
                if (OnCompleteNames != null)
                    OnCompleteNames();
                break;
            case ("re-init"):
                string ac_string_RI = System.Text.Encoding.UTF8.GetString(message[1]);
                int ac_int_RI = local_active_simulation;
                int.TryParse(ac_string_RI, out ac_int_RI);
                Debug.Log("active channel in re-init message: " + ac_int_RI);
                if (ac_int_RI != activeSimFromZMQ)
                {
                    //pulling init info from wrong channel.
                    reinitializing = false;
                }

                if (OnHoomdStartup != null)
                    OnHoomdStartup();

                break;
        }

        return false;

    }

    private void QueryChannelPublisher()
    {
        List<byte[]> message = null;

        bool recieved = activeChannelSocket.TryReceiveMultipartBytes(waitTime, ref message, 2);

        if (!recieved || message == null)
        {
            return;
        }

        string messageType = System.Text.Encoding.UTF8.GetString(message[0]);

        if (messageType == "channel-update")
        {
            string active_channel_string = System.Text.Encoding.UTF8.GetString(message[1]);
            int.TryParse(active_channel_string, out activeSimFromZMQ);
            Debug.Log("Active channel according to broker: " + activeSimFromZMQ + ". Active channel according to client: " + local_active_simulation);
            if (activeSimFromZMQ != local_active_simulation)
            {
                reinitializing = true;
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (frame_count % 100 == 0)
        {
            ConnectToACSocket();
            QueryChannelPublisher();
            DisconnectFromACSocket();
        }


        //if (are_channels_synced)
        //{
        //    initialized_on_correct_channel = true;
        //}
        //else
        //{
        //    reinitializing = true;
        //}

        if (reinitializing)
        {

            bool reinitialized = QueryInitializationPublisher();
            if (!reinitialized)
            {
                // Debug.Log("local and zmq channel mismatch yet unable to get re-init data from publisher.");
                //  reinitializing = true;
            }
            else
            {
                local_active_simulation = activeSimFromZMQ;
                reinitializing = false;
                DisconnectFromInitSocket();
            }

            return;
        }

        //only try to get positions if we have the correct particle names and bond data.
        while (true)
        {
            List<byte[]> message = null;
            bool recieved = subscriberSocket.TryReceiveMultipartBytes(waitTime, ref message, 2);
            if (!recieved || message == null)
            {
                frames_since_last_msg_from_publisher++;
                //Debug.Log("frames since last msg from publisher in return: " + frames_since_last_msg_from_publisher);
                return;
            }

            if (frames_since_last_msg_from_publisher > 0)
            {
                Debug.Log("frames since last msg from publisher: " + frames_since_last_msg_from_publisher);
                frames_since_last_msg_from_publisher = 0;
            }

            string activeSimFromZMQ_Str = System.Text.Encoding.UTF8.GetString(message[0]);
            int.TryParse(activeSimFromZMQ_Str, out activeSimFromZMQ);
            //This is here just to make sure that we don't try to use positions from a channel we aren't initialized for
            if (activeSimFromZMQ != local_active_simulation)
            {
                Debug.Log("Client thinks the active channel is " + local_active_simulation + " but it is actually " + activeSimFromZMQ + " so let's reinit");
                reinitializing = true;
                return;
            }

            string messageType = System.Text.Encoding.UTF8.GetString(message[1]);
            if (messageType == "frame-complete")
            {
                updates += 1;
                num_framecompletes_from_broker++;
                //Debug.Log("number of frame-completes from broker: " + num_framecompletes_from_broker + " at frame " + frame_count);

                if (OnCompleteFrame != null)
                    OnCompleteFrame();
                break;
            }

            var buf = new ByteBuffer(message[2]);
            var frame = Frame.GetRootAsFrame(buf);
            if (OnNewFrame != null)
                OnNewFrame(frame);

        }

        //after a break we can assume that we are at a 'frame-complete'

        //if (activeSimFromZMQ != local_active_simulation)
        //{
        //    // Debug.Log("client locally has active sim as " + local_active_simulation + ". Broker says its " + activeSimFromZMQ);
        //    bool reinitialized = QueryInitializationPublisher();
        //    if (!reinitialized)
        //    {
        //        //Debug.Log("local and zmq channel mismatch yet unable to get re-init data from publisher.");
        //        reinitializing = true;
        //    }
        //    else
        //    {
        //        local_active_simulation = activeSimFromZMQ;
        //        reinitializing = false;
        //    }

        //}
        frame_count++;
    }


    private void ZMQStartUp()
    {
        // set-up sockets
        string downstream_port_address = BROKER_IP_ADDRESS + "5572";
        string initialization_port_address = BROKER_IP_ADDRESS + "5573";

        subscriberSocket = new SubscriberSocket();
        subscriberSocket.Connect(downstream_port_address);
        subscriberSocket.SubscribeToAnyTopic();
        Debug.Log("Subscriber Socket connected on " + downstream_port_address);
        PlayerPrefs.SetString("IPAddress", BROKER_IP_ADDRESS);

        ConnectToACSocket();
        ConnectToInitSocket();


    }

    private void ConnectToInitSocket()
    {
        string initialization_port_address = BROKER_IP_ADDRESS + "5573";
        initializationSocket = new SubscriberSocket();
        initializationSocket.Connect(initialization_port_address);
        initializationSocket.SubscribeToAnyTopic();
        Debug.Log("Initialization Socket connected on " + initialization_port_address);
        init_socket_connected = true;
    }

    private void DisconnectFromInitSocket()
    {

        initializationSocket.Close();
        initializationSocket.Dispose();
        Debug.Log("Closed initialization Socket.");
        init_socket_connected = false;

    }

    private void ConnectToACSocket()
    {
        string ac_socket_port_address = BROKER_IP_ADDRESS + "5574";
        activeChannelSocket = new SubscriberSocket();
        activeChannelSocket.Connect(ac_socket_port_address);
        activeChannelSocket.SubscribeToAnyTopic();
        //  Debug.Log("Active Channel Socket connected on " + ac_socket_port_address);

    }

    private void DisconnectFromACSocket()
    {
        activeChannelSocket.Close();
        activeChannelSocket.Dispose();
        //  Debug.Log("Closed active Channel Socket.");

    }

    public void setAllBondsRead(bool b)
    {
        all_bonds_read = b;
    }

    public bool getAllBondsRead()
    {
        return all_bonds_read;
    }
}