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
    private Camera mainCam;
    //[SerializeField]
    //ParticleSystemRenderer particleSystemRenderer;

    private System.TimeSpan waitTime = new System.TimeSpan(0, 0, 5);
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

    public Vector3 cam_pos;
    public Vector3 cam_rot;

    private float pos_step = 0.3f;

    bool in2DView = false;

    // Start is called before the first frame update
    void Start()
    {
        //vrCC always starts first because the execution order is locked in project settings.
        startUpInstructorSocket();

        //idx, type, ip, initialized?, active?
        sim_channel_data_list = new List<string[]>();
        client_data_list = new List<string[]>();

        mainCam = camera.GetComponent<Camera>();

     //   particleSystemRenderer.enabled = false;

    }

    // Update is called once per frame
    void Update()
    {
        int current_active_channel = active_channel;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            bool canvas_state = DebugCanvas.GetComponent<Canvas>().enabled;
            DebugCanvas.GetComponent<Canvas>().enabled = !canvas_state;
        } else if (Input.GetKeyDown(KeyCode.A))
        {
            current_active_channel = 0;
           // goTo3DSimView();
        } else if (Input.GetKeyDown(KeyCode.B))
        {
            current_active_channel = 1;
            //Debug.Log("B");
           // goTo3DSimView();
        } else if(Input.GetKeyDown(KeyCode.C))
        {
            current_active_channel = 2;
            //goTo2DSimView();
        } else if (Input.GetKeyDown(KeyCode.D))
        {
            current_active_channel = 3;
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            current_active_channel = 4;
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            vrCC.forceFPSToMatchHoomd = !vrCC.forceFPSToMatchHoomd;
        }
        //else if (Input.GetKeyDown(KeyCode.O))
        //{
        //    goTo2DSimView();
        //}
        //else if (Input.GetKeyDown(KeyCode.P))
        //{
        //    SwapRenderers();
        //}
        else if (Input.GetKeyDown(KeyCode.S))
        {
            mainCam.orthographicSize += 5.0f;
            mainCam.fieldOfView += 5.0f;
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            mainCam.orthographicSize -= 5.0f;
            mainCam.fieldOfView -= 5.0f;
        }
        //else if (Input.GetKeyDown(KeyCode.T))
        //{
        //    if (in2DView)
        //    {
        //        goTo3DSimView();
        //    }
        //    else
        //    {
        //        goTo2DSimView();
        //    }
        //}
        else if (Input.GetKeyDown(KeyCode.R))
        {
            if (in2DView)
            {
                goTo2DSimView();
            } else
            {
                goTo3DSimView();
            }
            
        }
        else if (Input.GetKeyDown(KeyCode.Z))
        {
            mainCam.orthographicSize = 35.0f;//reset only the zoom level;
            mainCam.fieldOfView = 60;
        } else if (Input.GetKeyDown(KeyCode.K))
        {
            camera.transform.position += new Vector3(0.0f,pos_step,0.0f);
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            camera.transform.position -= new Vector3(0.0f, pos_step, 0.0f);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            camera.transform.position += new Vector3(pos_step, 0.0f, 0.0f);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            camera.transform.position -= new Vector3(pos_step, 0.0f, 0.0f);
        } else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            camera.transform.position += new Vector3(0.0f, 0.0f, pos_step);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            camera.transform.position -= new Vector3(0.0f, 0.0f, pos_step);
        }

        if (current_active_channel != active_channel)
        {
            vrCC.forceFPSToMatchHoomd = false;
            simInterface.setTemperature = 0.15f;
            var sendMsg = new NetMQMessage();
            sendMsg.Append("ac-change");
            sendMsg.Append(current_active_channel.ToString());
            bool r = PairClient.TrySendMultipartMessage(waitTime, sendMsg);
            if (r)
            {
                vrCC.zmqCycleDown();
                active_channel = current_active_channel;
                Debug.Log("switched ac to " + current_active_channel);
                vrCC.zmqUp();
            }
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

    //private void SwapRenderers()
    //{
    //    bool pRend = particleSystemRenderer.enabled;
    //    particleSystemRenderer.enabled = !pRend;
    //    molSystem.ToggleMeshView(pRend);
    //}

    private void goTo2DSimView()
    {
        camera.transform.position = new Vector3(0, 1, -19.95f) + cam_pos;
        camera.transform.localRotation = Quaternion.Euler(90, 0, 0);
        mainCam.orthographic = true;
        mainCam.orthographicSize = 35.0f;
        env.SetActive(false);

       // particleSystemRenderer.enabled = true;
        molSystem.ToggleMeshView(false);
        in2DView = true;
    }

    private void goTo3DSimView()
    {
        molSystem.setScaleF(0.35f);
        camera.transform.position = new Vector3(0, 3, -10) + cam_pos;
        camera.transform.localRotation = Quaternion.Euler(0, 0, 0);
        mainCam.orthographic = false;
        mainCam.fieldOfView = 60;
       // mainCam.orthographicSize = 35.0f;
        env.SetActive(true);

      //  particleSystemRenderer.enabled = false;
        molSystem.ToggleMeshView(true);
        in2DView = false;
    }

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

        var sendMsg = new NetMQMessage();
        sendMsg.Append("first-msg-instructor");
        sendMsg.Append(vrCC.client_id);

        PairClient.TrySendMultipartMessage(waitTime, sendMsg);

    }

    private void cleanUpInstructorSocket()
    {
        var sendMsg = new NetMQMessage();
        sendMsg.Append("last-msg-instructor");
        sendMsg.Append(vrCC.client_id);

        PairClient.TrySendMultipartMessage(waitTime, sendMsg);

        PairClient.Close();
        PairClient.Dispose();
    }

    private void OnApplicationQuit()
    {
        cleanUpInstructorSocket();

    }
}
