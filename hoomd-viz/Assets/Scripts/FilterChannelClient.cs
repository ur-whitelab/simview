using System.Collections;
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
    public string BROKER_IP_ADDRESS = "tcp://10.4.10.185:";

    private string initialization_address;
    private string downstream_address;
    private string active_channel_socket_address;

    public delegate void NewFrameAction(Frame frame);
    public delegate void CompleteFrameAction();
    public delegate void SimulationUpdateAction(Dictionary<string, string> state);
    public event NewFrameAction OnNewFrame;
    public event CompleteFrameAction OnCompleteFrame;

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
    private System.TimeSpan lingerTime = new System.TimeSpan(0, 0, 0);

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
    int frame_init_socket_disconnected = 0;
    private bool recieved_active_channel = false;

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
        //Debug.Log("init msg type: " + messageType);
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
                if (ac_int_RI != activeSimFromZMQ)
                {
                    //pulling init info from wrong channel, so abort re-init sequence.
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
            recieved_active_channel = true;
            string active_channel_string = System.Text.Encoding.UTF8.GetString(message[1]);
            int.TryParse(active_channel_string, out activeSimFromZMQ);
            //Debug.Log("Active channel according to broker: " + activeSimFromZMQ + ". Active channel according to client: " + local_active_simulation + "fc: " + frame_count);
            if (activeSimFromZMQ != local_active_simulation)
            {
                reinitializing = true;
            }
        }
    }

    //private IEnumerator ActiveChannelRoutine()
    //{
    //    ConnectToACSocket();

    //    recieved_active_channel = false;
    //    while (!recieved_active_channel)
    //    {
    //        QueryChannelPublisher();
    //        yield return new WaitForSeconds(1.0f);
    //    }

    //    DisconnectFromACSocket();
    //}

    // Update is called once per frame
    void Update()
    {
        //if (frame_count % 100 == 0)
        //{
        //    //Debug.Log("frame count is 10 - starting acr if not already begun.");
        //    IEnumerator acr = ActiveChannelRoutine();
        //    StartCoroutine(acr);
        //}

        //Broker only sends an active channel message if the active chanenl changes. Clients only need to be aware that a channel switch occured - they
        //don't need to care about the specific channel numbers since they'll get init data no matter what.
        QueryChannelPublisher();


        if (reinitializing)
        {

            bool reinitialized = QueryInitializationPublisher();
            if (reinitialized)
            {
                local_active_simulation = activeSimFromZMQ;
                reinitializing = false;

                DisconnectFromInitSocket();
                //I could see this being an issue - maybe there could be an instance where I try to pull from the positions socket before
                //it has a chance to fully re-initialze? Maybe use the 'flag socket thing' from the ZMQ docs?
                DisconnectFromPositionsSocket();
                ConnectToPositionsSocket();
            }

            return;
        }

        //only try to get positions if we have the correct particle names and bond data.
        while (true && !reinitializing)
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
                //Debug.Log("frames since last msg from publisher: " + frames_since_last_msg_from_publisher);
                frames_since_last_msg_from_publisher = 0;
            }

            string activeSimFromZMQ_Str = System.Text.Encoding.UTF8.GetString(message[0]);
            int.TryParse(activeSimFromZMQ_Str, out activeSimFromZMQ);
            //This is here just to make sure that we don't try to use positions from a channel we aren't initialized for
            if (activeSimFromZMQ != local_active_simulation)
            {
                Debug.Log("Client thinks the active channel is " + local_active_simulation + " but it is actually " + activeSimFromZMQ + ", so let's reinit");
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

        frame_count++;
    }


    private void ZMQStartUp()
    {
        // set-up sockets
        downstream_address = BROKER_IP_ADDRESS + "5572";
        initialization_address = BROKER_IP_ADDRESS + "5573";
        active_channel_socket_address = BROKER_IP_ADDRESS + "5574";

        subscriberSocket = new SubscriberSocket();
        subscriberSocket.Connect(downstream_address);
        subscriberSocket.SubscribeToAnyTopic();

        subscriberSocket.Options.Linger = lingerTime;

        Debug.Log("Subscriber Socket connected on " + downstream_address);
        //PlayerPrefs.SetString("IPAddress", BROKER_IP_ADDRESS);

        //ConnectToACSocket();
        activeChannelSocket = new SubscriberSocket();
        activeChannelSocket.Connect(active_channel_socket_address);
        activeChannelSocket.SubscribeToAnyTopic();

        activeChannelSocket.Options.Linger = lingerTime;
        ConnectToInitSocket();
    }

    private void ConnectToInitSocket()
    {
        initializationSocket = new SubscriberSocket();
        initializationSocket.Connect(initialization_address);
        initializationSocket.SubscribeToAnyTopic();

        initializationSocket.Options.Linger = lingerTime;

        int frame_diff = frame_count - frame_init_socket_disconnected;
        Debug.Log("init socket last disconnected " + frame_diff + " frames ago.");

        init_socket_connected = true;
    }

    private void DisconnectFromInitSocket()
    {
        initializationSocket.Close();
        initializationSocket.Dispose();
        init_socket_connected = false;
        frame_init_socket_disconnected = frame_count;
    }

    //private void ConnectToACSocket()
    //{
    //    activeChannelSocket = new SubscriberSocket();
    //    activeChannelSocket.Connect(active_channel_socket_address);
    //    activeChannelSocket.SubscribeToAnyTopic();

    //    activeChannelSocket.Options.Linger = lingerTime;
    //}

    //private void DisconnectFromACSocket()
    //{
    //    activeChannelSocket.Close();
    //    activeChannelSocket.Dispose();
    //}

    private void ConnectToPositionsSocket()
    {
        subscriberSocket = new SubscriberSocket();
        subscriberSocket.Connect(downstream_address);
        subscriberSocket.SubscribeToAnyTopic();
    }

    private void DisconnectFromPositionsSocket()
    {
        subscriberSocket.Close();
        subscriberSocket.Dispose();
    }

    public void setAllBondsRead(bool b)
    {
        all_bonds_read = b;
    }

    public bool getAllBondsRead()
    {
        return all_bonds_read;
    }

#if UNITY_ANDROID

    private void OnApplicationPause(bool pause)
    {
        DisconnectFromPositionsSocket();
        DisconnectFromACSocket();
        DisconnectFromInitSocket();
    }

#endif

    private void OnApplicationQuit()
    {
        DisconnectFromPositionsSocket();
        //DisconnectFromACSocket();
        activeChannelSocket.Close();
        activeChannelSocket.Dispose();
        DisconnectFromInitSocket();
    }
}
