using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Gvr;

public class SceneManager : MonoBehaviour
{
    [SerializeField]
    private MoleculeSystemGPU molSystem;

    private GvrControllerInputDevice gvrController;

    private Vector2 prevTouchPos;
    private float scale_factor = 0.5f;

    bool onTouchPad = false;

    // Start is called before the first frame update
    void Start()
    {
        gvrController = GvrControllerInput.GetDevice(GvrControllerHand.Dominant);

        prevTouchPos = Vector2.zero;
    }

    // Update is called once per frame
    void Update()
    {
        if (gvrController.GetButtonDown(GvrControllerButton.TouchPadTouch))
        {
            prevTouchPos = gvrController.TouchPos;
            onTouchPad = true;
            Debug.Log(gvrController.TouchPos);

        } else if (gvrController.GetButtonUp(GvrControllerButton.TouchPadTouch))
        {
            //prevTouchPos = Vector2.zero;
            onTouchPad = false;
        } else if (gvrController.GetButtonDown(GvrControllerButton.Trigger))
        {
            molSystem.incrementScaleF(0.01f);
        } else if (gvrController.GetButtonDown(GvrControllerButton.Trigger))
        {
            molSystem.incrementScaleF(-0.01f);
        }

        if (onTouchPad)
        {
            Vector2 touchPos = gvrController.TouchPos;
            Vector2 deltaTouchPos = (touchPos - prevTouchPos) * scale_factor;

            molSystem.SetPosOffset(new Vector3(deltaTouchPos.x, 0, deltaTouchPos.y));

            prevTouchPos = touchPos;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            molSystem.incrementScaleF(-0.01f);

        }

    }
}
