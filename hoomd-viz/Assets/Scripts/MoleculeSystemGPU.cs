using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HZMsg;

public class MoleculeSystemGPU : MonoBehaviour
{
    [SerializeField]
    private Transform atomPrefab;
    [SerializeField]
    private Transform bondPrefab;
    [SerializeField]
    private vrCommClient cc;

    private Transform[] moleculeTransforms;
    private Vector3[] frameUpdatePositions;
    private bool[] activeMolecules;

    private int max_num_molecules = 4000;
    private float init_radius = 50f;
    private float bond_length = 0.075f;

    [SerializeField]
    private Vector3 posOffset;

    [SerializeField]
    private float scaleF = 0.1f;

    private float last_graphics_update_time;
    private int last_graphics_update_frameCount;
    private float total_fps_sum;
    private float num_graphics_updates;

    private List<Vector3Int> mBonds;
    
    private bool all_bonds_read;
    // Start is called before the first frame update
    void Start()
    {
        cc.OnNewFrame += MolSysProcessFrameUpdate;
        cc.OnCompleteFrame += MolSysEndFrameUpdate;

        cc.OnNewBondFrame += MolSysProcessBondFrameUpdate;
        cc.OnCompleteBondFrame += MolSysProcessBondFrameComplete;

        mBonds = new List<Vector3Int>();
        all_bonds_read = false;

        posOffset = new Vector3(0, 2.2f, 3.14f);

        moleculeTransforms = new Transform[max_num_molecules];
        frameUpdatePositions = new Vector3[max_num_molecules];
        activeMolecules = new bool[max_num_molecules];

        last_graphics_update_time = 0.0f;
        last_graphics_update_frameCount = 0;
        total_fps_sum = 0.0f;
        num_graphics_updates = 0.0f;

        InitSystem();
    }

    private void MolSysProcessFrameUpdate(Frame frame)
    {
        for (int i = frame.I; i < frame.I + frame.N; i++)
        {
            frameUpdatePositions[i] = new Vector3(frame.Positions(i - frame.I).Value.X * scaleF,
                                                  frame.Positions(i - frame.I).Value.W * scaleF,
                                                  frame.Positions(i - frame.I).Value.Y * scaleF) + posOffset;
            activeMolecules[i] = true;
        }
    }

    private void MolSysEndFrameUpdate()
    {
        num_graphics_updates += 1.0f;
        //update graphics
        for (int i = 0; i < max_num_molecules; i++)
        {
            moleculeTransforms[i].position = frameUpdatePositions[i];

            if ((scaleF < 0.07f && i % 2 == 0) || !activeMolecules[i])
            {
                moleculeTransforms[i].gameObject.SetActive(false);
            } else
            {
                moleculeTransforms[i].gameObject.SetActive(true);
            }
        }
        activeMolecules = new bool[max_num_molecules]; //reset active mol array.

        float current_graphics_update_time = Time.time;
        float current_graphics_update_frameCount = Time.frameCount;

        float time_delta = current_graphics_update_time - last_graphics_update_time;
        float frame_delta = current_graphics_update_frameCount - last_graphics_update_frameCount;

        float delta_fps = frame_delta / time_delta;

        total_fps_sum += delta_fps;
        float avg_fps = total_fps_sum / num_graphics_updates;

        if (Mathf.Abs(delta_fps - avg_fps) >= 10.0f)
        {
            Debug.Log("average fps: " + avg_fps);
            Debug.Log("frames since last graphics update: " + frame_delta);
            Debug.Log("seconds since last graphics update: " + time_delta);
            Debug.Log("graphics fps: " + delta_fps);
        }

        last_graphics_update_time = Time.time;
        last_graphics_update_frameCount = Time.frameCount;
    }

    private void MolSysProcessBondFrameUpdate(Frame frame)
    {
        for (int i = frame.I; i < frame.I + frame.N; i++)
        {
            //(first particle idx, second particle idx, bond type)
            Vector3Int bond_data = new Vector3Int(frame.Bonds(i - frame.I).Value.A,
                                                  frame.Bonds(i - frame.I).Value.B,
                                                  frame.Bonds(i - frame.I).Value.T);

            mBonds.Add(bond_data);
        }
    }

    private void MolSysProcessBondFrameComplete()
    {
        Debug.Log(mBonds.Count + " bonds read in total.");

        all_bonds_read = true;
    }

    private void InitSystem()
    {

        if (!all_bonds_read)
        {
            Debug.Log("Trying to initialize molecule system without all bond data being sent to Unity!");
        }

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        for (int i = 0; i < max_num_molecules; i++)
        {

            moleculeTransforms[i] = Instantiate(atomPrefab);
            moleculeTransforms[i].position = transform.position;

            moleculeTransforms[i].SetParent(transform);

            // Transform tC = Instantiate(atomPrefab);
            //Transform tO = Instantiate(atomPrefab);

            //tC.localPosition = Random.insideUnitSphere * init_radius * 0.1f + posOffset;
            //tO.localPosition = Random.insideUnitSphere * init_radius * 0.1f + posOffset;
            // tC.localPosition = molParent.transform.position - new Vector3(bond_length / 2f, 0, 0);
            // tO.localPosition = molParent.transform.position + new Vector3(bond_length / 2f, 0, 0);

            //Transform tBond = Instantiate(bondPrefab);
            //tBond.localPosition = (tC.localPosition + tO.localPosition) / 2f;

            //tBond.transform.SetParent(molParent.transform);
            //tC.SetParent(molParent.transform);
            //tO.SetParent(molParent.transform);

            properties.SetColor("_Color", new Color(1.0f, 0.1f, 0.6f));

            MeshRenderer r = moleculeTransforms[i].GetComponent<MeshRenderer>();
            if (r)
            {
                r.SetPropertyBlock(properties);
            }
            else
            {
                for (int ci = 0; ci < moleculeTransforms[i].childCount; ci++)
                {
                    r = moleculeTransforms[i].GetChild(ci).GetComponent<MeshRenderer>();
                    if (r)
                    {
                        r.SetPropertyBlock(properties);
                    }
                }
            }
            moleculeTransforms[i].gameObject.SetActive(false);

        }
    }

    private void gpuSetColor(int idx, Color newColor)
    {
        MaterialPropertyBlock properties = new MaterialPropertyBlock();

        properties.SetColor("_Color", newColor);

        MeshRenderer r = moleculeTransforms[idx].GetComponent<MeshRenderer>();
        if (r)
        {
            r.SetPropertyBlock(properties);
        }
        else
        {
            for (int ci = 0; ci < moleculeTransforms[idx].childCount; ci++)
            {
                r = moleculeTransforms[idx].GetChild(ci).GetComponent<MeshRenderer>();
                if (r)
                {
                    r.SetPropertyBlock(properties);
                }
            }
        }
    }

    public void SetPosOffset(Vector3 touchDelta)
    {
        posOffset += touchDelta;

    }

    public void incrementScaleF(float scaleDelta)
    {
        scaleF += scaleDelta;
        scaleF = Mathf.Clamp(scaleF, 0.02f, 0.1f);
    }

}
