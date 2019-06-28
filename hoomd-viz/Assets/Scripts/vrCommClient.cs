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
    public string Server_Macbook_UR_RC_GUEST = "tcp://10.2.25.68:5556";

    public delegate void NewFrameAction(Frame frame);
    public delegate void CompleteFrameAction();
    public delegate void SimulationUpdateAction(Dictionary<string, string> state);
    public event NewFrameAction OnNewFrame;
    public event CompleteFrameAction OnCompleteFrame;
    public event SimulationUpdateAction OnSimulationUpdate;
    //  private PairSocket FrameClient;
    private DealerSocket FrameClient;
    //  private NetMQPoller poll;


    private System.TimeSpan waitTime = new System.TimeSpan(0, 0, 0);

    private string sendMsgStr = "{}";
    private int updates = 0;

    int wait_for_hoomd_count = 30;

    string client_id = "1";

    // Start is called before the first frame update
    void Start()
    {
#if UNITY_ANDROID

        client_id = SystemInfo.deviceUniqueIdentifier;
#endif

        Debug.Log("client id: " + client_id);

        ForceDotNet.Force();
        // set-up sockets and poller
        //FrameClient = new PairSocket();
        FrameClient = new DealerSocket();
        FrameClient.Options.Identity = System.Text.Encoding.UTF8.GetBytes("client-" + client_id);
        FrameClient.Connect(Server_Macbook_UR_RC_GUEST);
        Debug.Log("Socket connected on " + Server_Macbook_UR_RC_GUEST);
        FrameClient.SendFrame(System.Text.Encoding.UTF8.GetBytes("first-msg"));

    }

    public void SetMessage(Dictionary<string, string> msg)
    {
        sendMsgStr = JsonConvert.SerializeObject(msg, Formatting.Indented);
    }

    // Update is called once per frame
    void Update()
    {
        List<byte[]> msg = null;

        FrameClient.ReceiveMultipartBytes(ref msg, 2);

        if (msg == null)
        {
            Debug.Log("returning update becuase msg is null");
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
                        OnSimulationUpdate(values);
                    }
                }
                // now send state update
                var sendMsg = new NetMQMessage();
                sendMsg.Append("simulation-update");
                sendMsg.Append(sendMsgStr);
                FrameClient.SendMultipartMessage(sendMsg);

                sendMsgStr = "{}";
                updates = 0;

                break;
        }
    }


    void OnDestroy()
    {
        //     poll.Stop();

        FrameClient.Close();
        FrameClient.Dispose();
    }
}

