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

    // Start is called before the first frame update
    void Start()
    {
        cc.OnNewFrame += MolSysProcessFrameUpdate;
        cc.OnCompleteFrame += MolSysEndFrameUpdate;

        posOffset = new Vector3(0, 2.2f, 3.14f);

        moleculeTransforms = new Transform[max_num_molecules];
        frameUpdatePositions = new Vector3[max_num_molecules];
        activeMolecules = new bool[max_num_molecules];

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
    }

    private void InitSystem()
    {
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

            // molParent.SetActive(false);

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
