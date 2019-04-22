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
        Debug.Log("Socket connected");
        FramePoller = new NetMQPoller { FrameClient };
        FrameResponseTask = new TaskCompletionSource<byte[]>();


        // create callback for when socket is ready
        // This code will probably die in mobile phones due to use of threading (?) and/or Asyncio
        FrameClient.ReceiveReady += (s, a) =>
        {
            Debug.Log("Message received!");
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
            if (OnNewFrame != null)
                OnNewFrame(frame);
            FrameResponseTask = new TaskCompletionSource<byte[]>();
        }
    }

    void onDestroy()
    {
        try
        {
            FramePoller.StopAsync();
        }

        catch
        {
            UnityEngine.Debug.Log("Tried to stopasync while the poller wasn't running! Oops.");
        }
        FramePoller.Dispose();
        FrameClient.Close();
        FrameClient.Dispose();
    }

    void OnApplicationQuit()//cleanup
    {
        try
        {
            FramePoller.StopAsync();
        }

        catch
        {
            UnityEngine.Debug.Log("Tried to stopasync while the poller wasn't running! Oops.");
        }
        FramePoller.Dispose();
        FrameClient.Close();
        FrameClient.Dispose();
    }
}

