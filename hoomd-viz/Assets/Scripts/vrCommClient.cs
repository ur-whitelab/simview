using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlatBuffers;
using HZMsg;
//using System.Threading.Tasks;
using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;

public class vrCommClient : MonoBehaviour
{

    [Tooltip("Follows ZeroMQ syntax")]

    //public string ServerUri = "tcp://localhost:5556";
    public string Server_Macbook_UR_RC_GUEST = "tcp://192.168.1.168:";

    public delegate void NewFrameAction(Frame frame);
    public delegate void CompleteFrameAction();
    public delegate void SimulationUpdateAction(Dictionary<string, string> state);
    public event NewFrameAction OnNewFrame;
    public event CompleteFrameAction OnCompleteFrame;
    public event SimulationUpdateAction OnSimulationUpdate;
    private System.TimeSpan waitTime = new System.TimeSpan(0, 0, 0);

    private SubscriberSocket SubClient;
    private DealerSocket FrameClient;

    private string sendMsgStr = "{}";

    string client_id = "0";

    // Start is called before the first frame update
    void Start()
    {

#if UNITY_ANDROID

        client_id = SystemInfo.deviceUniqueIdentifier;
#endif

        Debug.Log("client id: " + client_id);

        ForceDotNet.Force();

        // set-up sockets
        string upstream_port_address = Server_Macbook_UR_RC_GUEST + "5556";
        string downstream_port_address = Server_Macbook_UR_RC_GUEST + "5559";

        FrameClient = new DealerSocket();
        FrameClient.Options.Identity = System.Text.Encoding.UTF8.GetBytes("client-" + client_id);
        FrameClient.Connect(upstream_port_address);
        Debug.Log("Dealer Socket connected on " + upstream_port_address);
        FrameClient.SendFrame(System.Text.Encoding.UTF8.GetBytes("first-msg"));

        SubClient = new SubscriberSocket();
        SubClient.Connect(downstream_port_address);
        SubClient.SubscribeToAnyTopic();
        Debug.Log("Subscriber Socket connected on " + downstream_port_address);
    }

    public void SetMessage(Dictionary<string, string> msg)
    {
        sendMsgStr = JsonConvert.SerializeObject(msg, Formatting.Indented);
    }

    // Update is called once per frame
    void Update()
    {
        List<byte[]> msg = null;

        bool received = SubClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);

        if (msg == null || !received)
        {
            return;
        }

        string msgType = System.Text.Encoding.UTF8.GetString(msg[0]);

        switch (msgType)
        {
            case ("frame-update"):

                var buf = new ByteBuffer(msg[1]);
                var frame = Frame.GetRootAsFrame(buf);
                if (OnNewFrame != null)
                    OnNewFrame(frame);
                break;

            case ("frame-complete"):

                if (OnCompleteFrame != null)
                    OnCompleteFrame();
                break;

            case ("state-update"):

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
                FrameClient.SendMultipartMessage(sendMsg);

                sendMsgStr = "{}";

                break;

            default:
                Debug.Log("Unexpected msg type: " + msgType);
                break;
        }
    }

//#if UNITY_ANDROID

//    private void OnApplicationPause(bool pause)
//    {
//        FrameClient.SendFrame(System.Text.Encoding.UTF8.GetBytes("last-msg"));

//        FrameClient.Close();
//        FrameClient.Dispose();

//        SubClient.Close();
//        SubClient.Dispose();
//    }

//#endif


    private void OnApplicationQuit()
    {
        FrameClient.SendFrame(System.Text.Encoding.UTF8.GetBytes("last-msg"));

        FrameClient.Close();
        FrameClient.Dispose();

        SubClient.Close();
        SubClient.Dispose();
    }
}

