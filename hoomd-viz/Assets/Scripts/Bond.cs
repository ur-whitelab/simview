using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bond : MonoBehaviour
{
    public int a1 = -1;
    public int a2 = -1;
    public int type = -1;

    public GameObject atom1;
    public GameObject atom2;

    Vector3 base_scale = new Vector3(0.02f, 0.02f, 0.02f);

    void Start()
    {
            
    }

    // Update is called once per frame
    void Update()
    {
        if (a1 != -1 && a2 != -1 && type != -1)
        {
            //transform.position = 0.5f*(atom2.transform.position + atom1.transform.position);
            //transform.LookAt(atom2.transform);
            float width = atom1.transform.localScale.x / 2.5f;
            Vector3 pos_delta = atom2.transform.position - atom1.transform.position;
            Vector3 scale = new Vector3(width, pos_delta.magnitude / 2.0f, width);
            transform.position = atom1.transform.position + (pos_delta / 2.0f);
            transform.up = pos_delta;
            transform.localScale = scale;
        }
    }
}
