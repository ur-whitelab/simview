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
    private CommClient cc;

    private Transform[] moleculeTransforms;

    private int num_molecules = 4000;
    private float init_radius = 50f;
    private float bond_length = 0.075f;

    [SerializeField]
    private Vector3 posOffset;

    // Start is called before the first frame update
    void Start()
    { 
        cc.OnNewFrame += molProcessFrameUpdate;
        //cc.OnCompleteFrame += atomEndFrameUpdate;

        posOffset = new Vector3(0, 0, 3.14f);

        moleculeTransforms = new Transform[num_molecules];

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        for (int i = 0; i < num_molecules; i++)//just hardcoding in CO for now.
        {
            GameObject molParent = new GameObject();
            molParent.name = "Mol Parent";
            molParent.transform.localPosition = transform.position;
            molParent.transform.SetParent(transform);

            moleculeTransforms[i] = molParent.transform;

            Transform tC = Instantiate(atomPrefab);
            Transform tO = Instantiate(atomPrefab);

            //tC.localPosition = Random.insideUnitSphere * init_radius * 0.1f + posOffset;
            //tO.localPosition = Random.insideUnitSphere * init_radius * 0.1f + posOffset;
            tC.localPosition = molParent.transform.position - new Vector3(bond_length/2f, 0, 0);
            tO.localPosition = molParent.transform.position + new Vector3(bond_length/2f, 0, 0);

            Transform tBond = Instantiate(bondPrefab);
            tBond.localPosition = (tC.localPosition + tO.localPosition) / 2f;

            tBond.transform.SetParent(molParent.transform);
            tC.SetParent(molParent.transform);
            tO.SetParent(molParent.transform);

            properties.SetColor("_Color", Color.black);

            MeshRenderer r = tC.GetComponent<MeshRenderer>();
            if (r)
            {
                r.SetPropertyBlock(properties);
            }
            else
            {
                for (int ci = 0; ci < tC.childCount; ci++)
                {
                    r = tC.GetChild(ci).GetComponent<MeshRenderer>();
                    if (r)
                    {
                        r.SetPropertyBlock(properties);
                    }
                }
            }

            properties.SetColor("_Color", Color.red);

            r = tO.GetComponent<MeshRenderer>();
            if (r)
            {
                r.SetPropertyBlock(properties);
            }
            else
            {
                for (int ci = 0; ci < tO.childCount; ci++)
                {
                    r = tO.GetChild(ci).GetComponent<MeshRenderer>();
                    if (r)
                    {
                        r.SetPropertyBlock(properties);
                    }
                }
            }

           // molParent.SetActive(false);

        }

    }

    //private void Update()
    //{

    //    for (int i = 0; i < num_molecules; i++)
    //    {
    //        moleculeTransforms[i].position = Random.insideUnitSphere * init_radius * 0.1f + posOffset;
    //        //atomTransforms[i].position = Random.insideUnitSphere * radius * 0.1f + offset;
    //        // atomTransforms[i].GetComponent<MeshRenderer>().enabled = true;
    //    }

    //}

    private void molProcessFrameUpdate(Frame frame)
    {
        float scaleF = 0.1f;
        for (int i = frame.I; i < frame.I + frame.N; i++)
        {
            if (i % 2 == 0)
            {
                if (!moleculeTransforms[i].gameObject.activeInHierarchy) { moleculeTransforms[i].gameObject.SetActive(true); }
                moleculeTransforms[i].position = new Vector3(frame.Positions(i - frame.I).Value.X * scaleF, frame.Positions(i - frame.I).Value.W * scaleF, frame.Positions(i - frame.I).Value.Y * scaleF) + posOffset;
            }

        }


    }

    public void SetPosOffset(Vector3 touchDelta)
    {
        posOffset += touchDelta;
    }


}
