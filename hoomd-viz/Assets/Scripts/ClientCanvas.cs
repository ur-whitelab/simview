using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClientCanvas : MonoBehaviour
{
    public Text id_text;
    public Text status_text;

    // Start is called before the first frame update
    void Start()
    {
        string find_string = "DebugCanvas/" + gameObject.name;

        id_text = GameObject.Find(find_string + "/id").GetComponent<Text>();
        status_text = GameObject.Find(find_string + "/status").GetComponent<Text>();

        id_text.text = "";
        status_text.text = "";

    }

}
