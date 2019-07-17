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
    //public string Server_Macbook_UR_RC_GUEST = "tcp://10.4.2.3:";

    public string BROKER_IP_ADDRESS = "tcp://localhost:";

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



    private System.TimeSpan waitTime = new System.TimeSpan(0, 0, 0);

    private SubscriberSocket SubClient;
    private DealerSocket FrameClient;

    private string sendMsgStr = "{}";

    string client_id = "0";

    private int last_msg_not_rec_fc = 0;

    private bool all_bonds_read = false;

    // Start is called before the first frame update
    void Start()
    {

        client_id = SystemInfo.deviceUniqueIdentifier;

        Debug.Log("client id: " + client_id);

        ForceDotNet.Force();

        //BROKER_IP_ADDRESS = PlayerPrefs.GetString("IPAddress", "No-Value");

        // set-up sockets
        string upstream_port_address = BROKER_IP_ADDRESS + "5556";
        string downstream_port_address = BROKER_IP_ADDRESS + "5559";

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
        //bool received = false;

        //if (!all_bonds_read)
        //{
        //    //client has initialized after Hoomd or Hoomd isn't initialized yet so see if server has sent us bonds.
        //    //Either we'll get them or we will get them eventually once Hoomd starts for the first time.
        //    received = FrameClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        //}
        //else
        //{
        //    //normal execution of program.
        //    received = SubClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        //}

        if (msg == null || !received)
        {

           // int fc = Time.frameCount;

            //Debug.Log("Message not received " + Time.frameCount + ", frames since last msg skip: " + (fc - last_msg_not_rec_fc));

          //  last_msg_not_rec_fc = Time.frameCount;

            return;
        }

        string msgType = System.Text.Encoding.UTF8.GetString(msg[0]);

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
                FrameClient.SendMultipartMessage(sendMsg);

                sendMsgStr = "{}";

                break;

            case ("bonds-update"):
            {
                    //var buf = new ByteBuffer(msg[1]);
                    //var frame = Frame.GetRootAsFrame(buf);
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

            default:
                Debug.Log("Unexpected msg type: " + msgType);
                break;
        }
    }

    public void setAllBondsRead(bool b)
    {
        all_bonds_read = b;
    }

    public bool getAllBondsRead()
    {
        return all_bonds_read;
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

