using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Gvr;

public class SceneManager : MonoBehaviour
{
    GvrControllerInputDevice gvrController;
    // Start is called before the first frame update
    void Start()
    {
        gvrController = GvrControllerInput.GetDevice(GvrControllerHand.Dominant);
    }

    // Update is called once per frame
    void Update()
    {

       if (gvrController.GetButtonDown(GvrControllerButton.TouchPadTouch))
        { 
            Debug.Log(gvrController.TouchPos);
        }
    }
}
