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

public class CommClient : MonoBehaviour
{
    
    [Tooltip("Follows ZeroMQ syntax")]

    [SerializeField]
    private string ServerUri = "tcp://localhost:5556";

    [SerializeField]
    private string Server_Macbook_UR_RC_GUEST = "tcp://192.168.1.168:5556";

    public delegate void NewFrameAction(Frame frame);
    public delegate void CompleteFrameAction();
    public delegate void SimulationUpdateAction(Dictionary<string, string> state);
    public event NewFrameAction OnNewFrame;
    public event CompleteFrameAction OnCompleteFrame;
    public event SimulationUpdateAction OnSimulationUpdate;
    private DealerSocket FrameClient;    

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
        // set up sockets and poller
        //FrameClient = new PairSocket();
        FrameClient = new DealerSocket();
        FrameClient.Options.Identity = System.Text.Encoding.UTF8.GetBytes("client-" + client_id);
        FrameClient.Connect(Server_Macbook_UR_RC_GUEST);
        Debug.Log("Socket connected on " + Server_Macbook_UR_RC_GUEST);

        //tell broker id of this client
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

        //  bool received;
        while (true)
        {
           FrameClient.ReceiveMultipartBytes(ref msg, 2);

            if (msg == null)
            {
                //Application.Quit();
                break;
            }


            // read string
            string msgType = System.Text.Encoding.UTF8.GetString(msg[0]);
            if (msgType == "frame-complete")
            {
                updates += 1;
                if (OnCompleteFrame != null)
                    OnCompleteFrame();
                break;
            }

            var buf = new ByteBuffer(msg[1]);
            var frame = Frame.GetRootAsFrame(buf);
            if (OnNewFrame != null)
                OnNewFrame(frame);
        }

        if (updates % 10 == 0)
        {
            Debug.Log("before % 10");
            FrameClient.ReceiveMultipartBytes(ref msg, 2);

            Debug.Log("msgtype: " + System.Text.Encoding.UTF8.GetString(msg[0]));
            Debug.Log("after % 10");
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

        }
    }


    void OnDestroy()
    {
        FrameClient.Close();
        FrameClient.Dispose();
    }
}

