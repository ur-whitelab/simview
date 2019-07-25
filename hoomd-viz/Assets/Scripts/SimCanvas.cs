using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SimCanvas : MonoBehaviour
{
    public Text type_text;
    public Text idx_text;
    public Text ip_text;
    public Text status_text;

    // Start is called before the first frame update
    void Start()
    {
        string find_string = "DebugCanvas/" + gameObject.name;

        type_text = GameObject.Find(find_string + "/type").GetComponent<Text>();
        idx_text = GameObject.Find(find_string + "/sim_idx").GetComponent<Text>();
        ip_text = GameObject.Find(find_string + "/ip").GetComponent<Text>();
        status_text = GameObject.Find(find_string + "/status").GetComponent<Text>();

        type_text.text = "";
        idx_text.text = "";
        ip_text.text = "";
        status_text.text = "";

    }

}
