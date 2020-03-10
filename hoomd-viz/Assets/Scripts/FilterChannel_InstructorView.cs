using System.Collections;
using System.Collections.Generic;
using FlatBuffers;
using HZMsg;
using UnityEngine;
using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;

//Instructor view only script
//Used for upstream communication - currently only supports toggling the active channel
//TODO: add support for state changing from here.

public class FilterChannel_InstructorView : MonoBehaviour
{
    public string BROKER_IP_ADDRESS = "tcp://localhost:";

    private string upstream_address;
    private PairSocket upstreamSocket;

    private int num_channels = 1;//default to 1 channel.

    // Start is called before the first frame update
    void Start()
    {
        // set-up sockets
        upstream_address = BROKER_IP_ADDRESS + "5575";

        upstreamSocket = new PairSocket();
        upstreamSocket.Connect(upstream_address);

        num_channels = 2;

    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < num_channels; i++)
        {
            if (Input.GetKeyDown(i.ToString()))
            {
                Debug.Log("sending message to change to channel " + i.ToString());
                var sendMsg = new NetMQMessage();
                sendMsg.Append("channel-change");
                sendMsg.Append(i.ToString());
                upstreamSocket.TrySendMultipartMessage(new System.TimeSpan(10, 0, 0), sendMsg);
            }
        }



    }
}
