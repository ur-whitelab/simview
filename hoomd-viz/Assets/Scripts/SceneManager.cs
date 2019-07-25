using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlatBuffers;
using HZMsg;
using UnityEngine.UI;
//using System.Threading.Tasks;
using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;
public class SceneManager : MonoBehaviour
{
    [SerializeField]
    private MoleculeSystemGPU molSystem;
    [SerializeField]
    private vrCommClient vrCC;
    [SerializeField]
    private SimInterface simInterface;
	[SerializeField]
	private GameObject env;
	[SerializeField]
	private GameObject camera;

    private System.TimeSpan waitTime = new System.TimeSpan(0, 0, 0);
    private PairSocket PairClient;

    List<string[]> sim_channel_data_list;
    List<string[]> client_data_list;

    [SerializeField]
    List<ClientCanvas> clientCanvasList;

    [SerializeField]
    List<SimCanvas> simCanvasList;

    [SerializeField]
    GameObject DebugCanvas;

    private int active_channel = 0;

    // Start is called before the first frame update
    void Start()
    {
        //vrCC always starts first because the execution order is locked in project settings.
        startUpInstructorSocket();

        //idx, type, ip, initialized?, active?
        sim_channel_data_list = new List<string[]>();
        client_data_list = new List<string[]>();

    }

    // Update is called once per frame
    void Update()
    {
        int current_active_channel = active_channel;
        if (Input.GetKeyDown(KeyCode.D))
        {
            bool canvas_state = DebugCanvas.GetComponent<Canvas>().enabled;
            DebugCanvas.GetComponent<Canvas>().enabled = !canvas_state;
        } else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            current_active_channel = 1;
        } else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            current_active_channel = 0;
        } else if(Input.GetKeyDown(KeyCode.Alpha2))
        {
            current_active_channel = 2;
        } else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            current_active_channel = 3;
        }


        if (current_active_channel != active_channel)
        {
            var sendMsg = new NetMQMessage();
            sendMsg.Append("ac-change");
            sendMsg.Append(current_active_channel.ToString());
            bool r = PairClient.TrySendMultipartMessage(waitTime, sendMsg);
            if (r)
            {
                active_channel = current_active_channel;
                Debug.Log("switched ac to " + current_active_channel);
            }
        }
        //generalize this eventually
		if (active_channel == 2)
		{
			molSystem.setScaleF(0.1f);
            camera.transform.position = new Vector3(0, 1, -19.95f);
            env.SetActive(false);
		} else
		{
			molSystem.setScaleF(0.35f);
            camera.transform.position = new Vector3(0, 1, -10);
            env.SetActive(true);
        }

        //User inputs that are Unity side and don't need to be sent to the broker.
        //- change camera position
        //- change view mode; density vs normal vs big molecule
        //- Select index for big molecule
        //- Toggle angle display?
        //change state vars - technically it is broker related but we can just call a method from SimInterface.

        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    molSystem.incrementScaleF(-0.01f);

        //}

        //if (gvrController.GetButtonDown(GvrControllerButton.TouchPadButton))
        //{
        //    simInterface.resetStateVars();
        //}

        //Get debug info from broker
        //- list of simulations and if they are initialized and which one is active
        //- current ip address
        //-list of clients and if they're connected

        List<byte[]> msg = null;
        bool received = PairClient.TryReceiveMultipartBytes(waitTime, ref msg, 2);
        if (msg == null || !received)
        {
            return;
        }

        string msgType = System.Text.Encoding.UTF8.GetString(msg[0]);
        if (msgType == "debug-string")
        {
            string debug_content = System.Text.Encoding.UTF8.GetString(msg[1]);
            parseDebugStringToLists(debug_content);
            pushDebugDataToCanvas();
        }
        //Then send broker instructions
        //- switch active channel
    }
    //parses debug string from broker and populates sim_list and client_list
    private void parseDebugStringToLists(string full_debug_string)
    {
        sim_channel_data_list.Clear();
        client_data_list.Clear();

        string[] _lists = full_debug_string.Split('_');

        string sim_list = _lists[0];
        string client_list = _lists[1];

        string[] sims = sim_list.Split('|');
        foreach (var sim in sims)
        {
            string[] sim_data = sim.Split(',');
            //string[] sim_data = new string[5];
            sim_channel_data_list.Add(sim_data);
        }

        string[] clients = client_list.Split('|');
        foreach (var client in clients)
        {
            string[] client_data = client.Split(',');
            client_data_list.Add(client_data);
        }
    }

    private void pushDebugDataToCanvas()
    {
        for (int i = 0; i < sim_channel_data_list.Count; i++)
        {
            if (simCanvasList[i] != null)
            {
                if (sim_channel_data_list[i].Length >= 5)
                {
                    simCanvasList[i].idx_text.text = "simulation " + sim_channel_data_list[i][0];
                    simCanvasList[i].type_text.text = sim_channel_data_list[i][1];
                    simCanvasList[i].ip_text.text = sim_channel_data_list[i][2];
                    simCanvasList[i].status_text.text = sim_channel_data_list[i][4];
                }
            }
            
        }

        for (int i = 0; i < client_data_list.Count; i++)
        {
            if (clientCanvasList[i] != null)
            {
                if (client_data_list[i].Length >= 2)
                {
                    clientCanvasList[i].id_text.text = client_data_list[i][0];
                    clientCanvasList[i].status_text.text = client_data_list[i][1];
                }   
            }
        }

    }

    private void startUpInstructorSocket ()
	{
		string port_address = vrCC.BROKER_IP_ADDRESS + "5570";

        PairClient = new PairSocket();
        PairClient.Connect(port_address);

        Debug.Log("Instructor pair socket connected on " + port_address);

	}

    private void cleanUpInstructorSocket()
    {
        PairClient.Close();
        PairClient.Dispose();
    }

    private void OnApplicationQuit()
    {
        cleanUpInstructorSocket();
    }
}
