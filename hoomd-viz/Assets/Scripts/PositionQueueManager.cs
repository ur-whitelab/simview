using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HZMsg;

public class PositionQueueManager : MonoBehaviour
{
    private List<Vector3[]> positonQueue;

    //private Vector3[] firstPositions;
    //private Vector3[] secondPositions;
    private Vector3[] tmpPositions;

    private bool[] activeMolecules;

    private int num_positions_from_hoomd = -1;
    private int num_firstPositions = 0;
    private int num_secondPositions = 0;

    private float scale_factor = 0.0f;

    private float interpolationSpeed = 0.15f;
    private float interpolationStep = 0.0f;

    private bool arePositionArraysSetup = false;

    [SerializeField]
    private MoleculeSystemGPU moleculeSystem;
    [SerializeField]
    private vrCommClient vrCC;

    bool firstPositionsEmpty = true;

    // Start is called before the first frame update
    void Start()
    {
        vrCC.OnNewFrame += PosProcessFrameUpdate;
        vrCC.OnCompleteFrame += PosEndFrameUpdate;

        vrCC.OnCompleteNames += PosProcessNamesComplete;

        vrCC.OnHoomdStartup += PosPrepForNewHoomdSession;

        scale_factor = moleculeSystem.scaleF;

        positonQueue = new List<Vector3[]>();
    }

    // Update is called once per frame
    void Update()
    {
        //both position vectors are full so we can interpolate between them.
        //if (arePositionArraysSetup)
        //{
        //    //interpolate first->second...
        //    Vector3[] interpolatedPositions = new Vector3[num_positions_from_hoomd];

        //    float interpolationProgress = (float)interpolationStep * interpolationSpeed;
        //    Debug.Log("progress : " + interpolationProgress);
        //    for (int i = 0; i < num_positions_from_hoomd; i++)
        //    {
        //        interpolatedPositions[i] = Vector3.Lerp(firstPositions[i], secondPositions[i], interpolationProgress);
        //    }
        //    moleculeSystem.UpdatePositions(interpolatedPositions);
        //    if (System.Math.Abs(interpolationProgress - 1.0f) < 0.0001f)
        //    {
        //        firstPositions = secondPositions;
        //        //num_secondPositions = 0;

        //        interpolationStep = 0;
        //    }
        //    else
        //    {
        //        interpolationStep++;
        //    }

        //}

        if (positonQueue.Count >= 2)
        {
            float interpolationProgress = interpolationStep * interpolationSpeed;
            //Debug.Log("progress : " + interpolationProgress);

            Vector3[] interpolatedPositions = new Vector3[num_positions_from_hoomd];

            //guaranteed to be at least 1.
            int toInterpolateIdx = positonQueue.Count - 1;
           // Debug.Log("to interp idx: " + toInterpolateIdx);

            for (int i = 0; i < num_positions_from_hoomd; i++)
            {
                interpolatedPositions[i] = Vector3.Lerp(positonQueue[0][i], positonQueue[1][i], interpolationProgress);
            }

            moleculeSystem.UpdatePositions(interpolatedPositions);

            //done interpolating between these two frames.
            if (System.Math.Abs(interpolationProgress - 1.0f) <= 0.01f)
            {
                positonQueue.RemoveRange(0, 1);
                Debug.Log("removed " + toInterpolateIdx + " frames, " + positonQueue.Count + " remaining");
                interpolationStep = 0;
            }
            else
            {
                interpolationStep+=0.1f;
              //  Debug.Log("interp step: " + interpolationStep);
            }

        }

    }

    private void PosPrepForNewHoomdSession()
    {
        num_positions_from_hoomd = -1;
        num_firstPositions = 0;
        num_secondPositions = 0;

        positonQueue.Clear();
    }

    private void PosProcessNamesComplete()
    {
        //assume every particle has a position - I'm pretty sure this is a safe assumption.
        num_positions_from_hoomd = moleculeSystem.num_particles_from_hoomd;

        tmpPositions = new Vector3[num_positions_from_hoomd];
       // firstPositions = new Vector3[num_positions_from_hoomd];
       // secondPositions = new Vector3[num_positions_from_hoomd];

        Debug.Log("instantiating vectors in queue with size of " + num_positions_from_hoomd);

        activeMolecules = new bool[num_positions_from_hoomd];
    }

    private void PosProcessFrameUpdate(Frame frame)
    {

        for (int i = frame.I; i < frame.I + frame.N; i++)
        {
            //frameUpdatePositions[i] = new Vector3(frame.Positions(i - frame.I).Value.Y * scaleF,
            //                                     frame.Positions(i - frame.I).Value.X * scaleF,
            //                                   frame.Positions(i - frame.I).Value.W * scaleF) + posOffset;
            tmpPositions[i] = new Vector3(frame.Positions(i - frame.I).Value.X,
                                                  frame.Positions(i - frame.I).Value.Y,
                                                  frame.Positions(i - frame.I).Value.Z) * scale_factor;
            activeMolecules[i] = true;

        }
    }

    private void PosEndFrameUpdate()
    {

        positonQueue.Add(tmpPositions);
        Debug.Log(positonQueue.Count + " positions in queue.");

        ////update graphics
        //for (int i = 0; i < num_positions_from_hoomd; i++)
        //{

        //    //push to first positions for interpolation
        //    if (firstPositionsEmpty)
        //    {
        //        firstPositions[i] = tmpPositions[i];
        //        num_firstPositions++;
        //    } else
        //    {
        //        secondPositions[i] = tmpPositions[i];
        //        num_secondPositions++;
        //    }

        //   // moleculeTransforms[i].position = frameUpdatePositions[i];
        //   // molTransforms_Sprites[i].position = frameUpdatePositions[i];

        //    //if ((scaleF < 0.07f && i % 2 == 0) || !activeMolecules[i])
        //    //{
        //    //    moleculeTransforms[i].gameObject.SetActive(false);
        //    //    molTransforms_Sprites[i].gameObject.SetActive(false);
        //    //}
        //    //else
        //    //{
        //    //    moleculeTransforms[i].gameObject.SetActive(mesh_rend);
        //    //    molTransforms_Sprites[i].gameObject.SetActive(!mesh_rend);
        //    //}
        //}

        //if (num_firstPositions == num_positions_from_hoomd)
        //{
        //    firstPositionsEmpty = false;
            
        //} else
        //{
        //    Debug.Log("first positions still empty!");
        //}

        //if (!firstPositionsEmpty && num_firstPositions == num_secondPositions)
        //{
        //    arePositionArraysSetup = true;
        //}

        ////I think this occurs if a frame complete is called in the middle of an interp.
        //if (arePositionArraysSetup)
        //{
        //    firstPositions = secondPositions;
        //}

        activeMolecules = new bool[num_positions_from_hoomd]; //reset active mol array.


    }
}
