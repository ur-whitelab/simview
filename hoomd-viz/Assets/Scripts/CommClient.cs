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
    public string ServerUri = "tcp://127.0.0.1:8076";

    public delegate void NewFrameAction(Frame frame);
    public event NewFrameAction OnNewFrame;

    private TaskCompletionSource<byte[]> FrameResponseTask;
    private SubscriberSocket FrameClient;
    private NetMQPoller FramePoller;



    // Start is called before the first frame update
    void Start()
    {
        // set-up sockets and poller
        FrameClient = new SubscriberSocket();
        FrameClient.Subscribe("frame-update");
        FrameClient.Connect(ServerUri);

        FramePoller = new NetMQPoller { FrameClient };
        FrameResponseTask = new TaskCompletionSource<byte[]>();
    

        // create callback for when socket is ready
        // This code will probably die in mobile phones due to use of threading (?) and/or Asyncio
        FrameClient.ReceiveReady += (s, a) =>
        {
            var msg = a.Socket.ReceiveMultipartBytes();
            while (!FrameResponseTask.TrySetResult(msg[1])) ;
        };

        FramePoller.RunAsync();
    }

    // Update is called once per frame
    void Update()
    {
        if(FrameResponseTask.Task.IsCompleted)
        {
            // have new data
            var buf = new ByteBuffer(FrameResponseTask.Task.Result);
            var frame = Frame.GetRootAsFrame(buf);
            Debug.Log("Received message containing" + frame.N + " particles\n");
            if (OnNewFrame != null)
                OnNewFrame(frame);
        }
    }
}
