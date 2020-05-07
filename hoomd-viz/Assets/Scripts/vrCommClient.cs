using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatBuffers;
using HZMsg;
using UnityEngine.UI;
//using System.Threading.Tasks;
using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;

public class vrCommClient : MonoBehaviour
{

    [Tooltip("Follows ZeroMQ syntax")]

    //public string ServerUri = "tcp://localhost:5556";
    //public string Server_Macbook_UR_RC_GUEST = "tcp://10.4.2.3:";

    //public string BROKER_IP_ADDRESS = "tcp://ar-table.che.rochester.edu:";
    public string BROKER_IP_ADDRESS = "tcp://10.5.13.161:";

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

    [SerializeField]
    private GameObject ipCanvas;
    [SerializeField]
    private InputField ipInputField;

    private System.TimeSpan waitTime = new System.TimeSpan(0, 0, 0);
    private System.TimeSpan waitTime_forced = new System.TimeSpan(0, 0, 5);


    private SubscriberSocket SubClient;
    private DealerSocket FrameClient;

    private string sendMsgStr = "{}";

    public string client_id = "0";

    private int last_msg_not_rec_fc = 0;

    private bool all_bonds_read = false;

    bool zmq_initialized = false;

    //this will always be false for VR views but the instructor view has the ability to enable it.
    public bool forceFPSToMatchHoomd;
    private int updates = 0;

    private int frame_num = 0;

    // Start is called before the first frame update
    void Start()
    {
        forceFPSToMatchHoomd = false;

        client_id = SystemInfo.deviceUniqueIdentifier;

        Debug.Log("client id: " + client_id);

        ForceDotNet.Force();

        //BROKER_IP_ADDRESS = PlayerPrefs.GetString("IPAddress", "No-Value");
        //if (BROKER_IP_ADDRESS == "No-Value")
        //{
        //    BROKER_IP_ADDRESS = "tcp://localhost:";
        //}

        zmqStartUp();

    }

    public void SetMessage(Dictionary<string, string> msg)
    {
        sendMsgStr = JsonConvert.SerializeObject(msg, Formatting.Indented);
    }

    private void ProcessMessage()
    {

        List<byte[]> msg = null;
        //bool received = SubClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        bool received;

        List<byte[]> init_msg = null;
        bool init_recv = FrameClient.TryReceiveMultipartBytes(waitTime, ref init_msg, 2);
        if (init_msg != null)
        {
            string init_msgType = System.Text.Encoding.UTF8.GetString(init_msg[0]);
            Debug.Log("init msg type");
            switch (init_msgType)
            {
                case ("bonds-update"):
                    {
                        if (OnNewBondFrame != null)
                            OnNewBondFrame(System.Text.Encoding.UTF8.GetString(init_msg[1]));
                        break;
                    }

                case ("bonds-complete"):
                    if (OnCompleteBondFrame != null)
                        OnCompleteBondFrame();
                    break;

                case ("names-update"):
                    if (OnNewName != null)
                        OnNewName(System.Text.Encoding.UTF8.GetString(init_msg[1]));
                    break;

                case ("names-complete"):
                    if (OnCompleteNames != null)
                        OnCompleteNames();
                    break;
                case ("hoomd-startup"):
                    if (OnHoomdStartup != null)
                        OnHoomdStartup();
                    break;
            }
            
        }


        if (!all_bonds_read)
        {
            //client has initialized after Hoomd or Hoomd isn't initialized yet so see if server has sent us bonds.
            //Either we'll get them normally or we will get them eventually once Hoomd starts for the first time.
            // received = FrameClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
            received = false;
        }
        else
        {
            //normal execution of program.
            received = SubClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        }

        if (msg == null || !received)
        {

            // int fc = Time.frameCount;

            //Debug.Log("Message not received " + Time.frameCount + ", frames since last msg skip: " + (fc - last_msg_not_rec_fc));

            //  last_msg_not_rec_fc = Time.frameCount;

            return;
        }

        string msgType = System.Text.Encoding.UTF8.GetString(msg[0]);
        //        Debug.Log("message type: " + msgType + " received at frame " + Time.frameCount);

        switch (msgType)
        {
            case ("frame-update"):
                {
                    //Debug.Log("frame-update " + Time.frameCount);
                    var buf = new ByteBuffer(msg[1]);
                    var frame = Frame.GetRootAsFrame(buf);
                    if (OnNewFrame != null)
                        OnNewFrame(frame);
                    break;
                }

            case ("frame-complete"):
                //Debug.Log("frame-complete " + Time.frameCount);
                if (OnCompleteFrame != null)
                    OnCompleteFrame();
                break;

            case ("state-update"):
                // Debug.Log("state-update " + Time.frameCount);
                if (OnSimulationUpdate != null)
                {
                    string jsonString = System.Text.Encoding.UTF8.GetString(msg[1]);
                    if (jsonString.Length > 0)
                    {
                        var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                        //Debug.Log("values[temp]: " + values["temperature"]);
                        //Debug.Log("values[dens]: " + values["density"]);
                        OnSimulationUpdate(values);
                    }
                }
                // now send state update
                var sendMsg = new NetMQMessage();
                sendMsg.Append("simulation-update");
                sendMsg.Append(sendMsgStr);
                FrameClient.TrySendMultipartMessage(waitTime, sendMsg);

                sendMsgStr = "{}";

                break;

            case ("bonds-update"):
                {
                    if (OnNewBondFrame != null)
                        OnNewBondFrame(System.Text.Encoding.UTF8.GetString(msg[1]));
                    break;
                }

            case ("bonds-complete"):
                if (OnCompleteBondFrame != null)
                    OnCompleteBondFrame();
                break;

            case ("names-update"):
                if (OnNewName != null)
                    OnNewName(System.Text.Encoding.UTF8.GetString(msg[1]));
                break;

            case ("names-complete"):
                if (OnCompleteNames != null)
                    OnCompleteNames();
                break;
            case ("hoomd-startup"):
                if (OnHoomdStartup != null)
                    OnHoomdStartup();
                break;

            default:
                Debug.Log("Unexpected msg type: " + msgType);
                break;
        }
    }

    //Essentially the original CommClient with added message types.
    private void ProcessMessage_ForcedFPS()
    {
        List<byte[]> msg = null;
        //bool received = SubClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        bool received;
        if (!all_bonds_read)
        {
            //client has initialized after Hoomd or Hoomd isn't initialized yet so see if server has sent us bonds.
            //Either we'll get them normally or we will get them eventually once Hoomd starts for the first time.
            received = FrameClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        }
        else
        {
            //normal execution of program.
            received = SubClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        }

        if (msg == null || !received)
        {

            // int fc = Time.frameCount;

            //Debug.Log("Message not received " + Time.frameCount + ", frames since last msg skip: " + (fc - last_msg_not_rec_fc));

            //  last_msg_not_rec_fc = Time.frameCount;

            return;
        }

        string msgType = System.Text.Encoding.UTF8.GetString(msg[0]);
        //        Debug.Log("message type: " + msgType + " received at frame " + Time.frameCount);

        switch (msgType)
        {
            case ("frame-update"):
                {
                    var buf = new ByteBuffer(msg[1]);
                    var frame = Frame.GetRootAsFrame(buf);
                    if (OnNewFrame != null)
                        OnNewFrame(frame);

                    while (true)
                    {
                        received = SubClient.TryReceiveMultipartBytes(waitTime_forced, ref msg, 2);
                        if (!received)
                        {
                            // had timeout problem
                            Application.Quit();
                            break;
                        }
                        // read string
                        string loc_msgType = System.Text.Encoding.UTF8.GetString(msg[0]);
                        if (loc_msgType == "frame-complete")
                        {
                            updates += 1;
                            if (OnCompleteFrame != null)
                                OnCompleteFrame();
                            break;
                        }
                        if (loc_msgType == "frame-update")
                        {
                            var _buf = new ByteBuffer(msg[1]);
                            var _frame = Frame.GetRootAsFrame(_buf);
                            if (OnNewFrame != null)
                                OnNewFrame(_frame);
                        }
                       
                    }

                    break;
                }

            case ("state-update"):
                // Debug.Log("state-update " + Time.frameCount);
                if (OnSimulationUpdate != null)
                {
                    string jsonString = System.Text.Encoding.UTF8.GetString(msg[1]);
                    if (jsonString.Length > 0)
                    {
                        var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                        //Debug.Log("values[temp]: " + values["temperature"]);
                        //Debug.Log("values[dens]: " + values["density"]);
                        OnSimulationUpdate(values);
                    }
                }
                // now send state update
                var sendMsg = new NetMQMessage();
                sendMsg.Append("simulation-update");
                sendMsg.Append(sendMsgStr);
                //FrameClient.TrySendMultipartMessage(waitTime, sendMsg);

                sendMsgStr = "{}";

                break;

            case ("bonds-update"):
                {
                    if (OnNewBondFrame != null)
                        OnNewBondFrame(System.Text.Encoding.UTF8.GetString(msg[1]));
                    break;
                }

            case ("bonds-complete"):
                if (OnCompleteBondFrame != null)
                    OnCompleteBondFrame();
                break;

            case ("names-update"):
                if (OnNewName != null)
                    OnNewName(System.Text.Encoding.UTF8.GetString(msg[1]));
                break;

            case ("names-complete"):
                if (OnCompleteNames != null)
                    OnCompleteNames();
                break;
            case ("hoomd-startup"):
                if (OnHoomdStartup != null)
                    OnHoomdStartup();
                break;

            default:
                Debug.Log("Unexpected msg type: " + msgType);
                break;
        }


    }

    // Update is called once per frame
    void Update()
    {
        if (forceFPSToMatchHoomd)
        {
            ProcessMessage_ForcedFPS();
        } else
        {
            ProcessMessage();
        }

        //if (frame_num % 100 == 0)
        //{
        //    zmqCycleDown();
        //    zmqUp();
        //}

        frame_num++;
    }

    public void setAllBondsRead(bool b)
    {
        all_bonds_read = b;
    }

    public bool getAllBondsRead()
    {
        return all_bonds_read;
    }

    public void startHoomdLJSim()
    {

    }

    public void startHoomdWaterSim()
    {

    }

    public void enableIPMenu()
    {
        ipCanvas.SetActive( !ipCanvas.activeInHierarchy );
    }

    public void newIpAddress()
    {
        ipCanvas.SetActive(false);
        zmqCleanUp();
        BROKER_IP_ADDRESS = ipInputField.text;
        zmqStartUp();
        Debug.Log("menu done");

    }

    private void zmqStartUp()
    {
        // set up sockets
        string upstream_port_address = BROKER_IP_ADDRESS + "5571";
        string downstream_port_address = BROKER_IP_ADDRESS + "5572";

        FrameClient = new DealerSocket();
        FrameClient.Options.Identity = System.Text.Encoding.UTF8.GetBytes(client_id);
        FrameClient.Connect(upstream_port_address);
        Debug.Log("Dealer Socket connected on " + upstream_port_address);
        FrameClient.SendFrame(System.Text.Encoding.UTF8.GetBytes("first-msg"));

        SubClient = new SubscriberSocket();
        SubClient.Connect(downstream_port_address);
        SubClient.SubscribeToAnyTopic();
        Debug.Log("Subscriber Socket connected on " + downstream_port_address);
        PlayerPrefs.SetString("IPAddress", BROKER_IP_ADDRESS);

    }

    private void zmqCleanUp()
    {
        FrameClient.Close();
        FrameClient.Dispose();

        SubClient.Close();
        SubClient.Dispose();

        zmq_initialized = false;
    }

    public void zmqCycleDown()
    {
        SubClient.Close();
        SubClient.Dispose();
    }

    public void zmqUp()
    {
        string downstream_port_address = BROKER_IP_ADDRESS + "5572";

        SubClient = new SubscriberSocket();
        SubClient.Connect(downstream_port_address);
        SubClient.SubscribeToAnyTopic();
        Debug.Log("Subscriber Socket connected on " + downstream_port_address);
        PlayerPrefs.SetString("IPAddress", BROKER_IP_ADDRESS);
    }
    //#if UNITY_ANDROID

    //    private void OnApplicationPause(bool pause)
    //    {
    //        FrameClient.SendFrame(System.Text.Encoding.UTF8.GetBytes("last-msg"));
    //        zmqCleanUp();
    //    }

    //#endif

    private void OnApplicationQuit()
    {
        Debug.Log("app quitting..");
        FrameClient.SendFrame(System.Text.Encoding.UTF8.GetBytes("last-msg"));
        zmqCleanUp();
    }
}

