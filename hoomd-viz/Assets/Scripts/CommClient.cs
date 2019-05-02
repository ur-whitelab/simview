using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlatBuffers;
using HZMsg;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

public class CommClient : MonoBehaviour
{

    [Tooltip("Follows ZeroMQ syntax")]
    public string ServerUri = "tcp://localhost:5000";

    public delegate void NewFrameAction(Frame frame);
    public event NewFrameAction OnNewFrame;
    private SubscriberSocket FrameClient;
    private Frame.Frame lastMessage;



    // Start is called before the first frame update
    void Start()
    {
        // set-up sockets and poller
        FrameClient = new PairSocket();
        FrameClient.Connect(ServerUri);
        Debug.Log("Socket connected on " + ServerUri);
        lastMessage = null;

    }

    // Update is called once per frame
    void Update()
    {
        // treat last message if necessary
        if (OnNewFrame != null && lastMessage != null)
            OnNewFrame(lastMessage);

        while(true) {
            Msg msg;
            var received = a.Socket.TryReceiveMultipartBytes(1, msg);
            if(!received) {
                // had timeout problem
                lastMessage = null;
                break;
            }
            var buf = new ByteBuffer(msg[1]);
            var frame = Frame.GetRootAsFrame(buf);
            if (lastMessage != null && frame.time != lastMessage.time) {
                // new timestep
                lastMessage = frame;
                break;
            }
            if (OnNewFrame != null)
                OnNewFrame(frame);
        }
    }

    void onDestroy()
    {
        FrameClient.Dispose();
    }
}

