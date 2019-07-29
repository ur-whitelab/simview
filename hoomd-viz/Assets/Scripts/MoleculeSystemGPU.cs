using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HZMsg;

public class MoleculeSystemGPU : MonoBehaviour
{
    [SerializeField]
    private Transform atomPrefab;
    [SerializeField]
    private Transform atomPrefabSprite;
    [SerializeField]
    private Transform bondPrefab;
    [SerializeField]
    private vrCommClient cc;

    private Transform[] moleculeTransforms;
    private Transform[] molTransforms_Sprites;
    private Vector3[] frameUpdatePositions;
    private bool[] activeMolecules;

    private int num_positions_from_hoomd = 0;
    private int num_particles_from_hoomd = 0;
    private float init_radius = 50f;
    private float bond_length = 0.075f;

    [SerializeField]
    private Vector3 posOffset;

    [SerializeField]
    private float scaleF = 1.0f;

    private float last_graphics_update_time;
    private int last_graphics_update_frameCount;
    private float total_fps_sum;
    private float num_graphics_updates;

    private List<Vector3Int> mBonds;
    int bond_data_container_size = 3;//a1,a2,type.

    private List<string> particleNames;
    private int num_names_read = 0;

    //simulation viz box dimensions.
    private Vector3 max_particle_position;

    bool mesh_rend = true;

    // Start is called before the first frame update
    void Start()
    {
        cc.OnNewFrame += MolSysProcessFrameUpdate;
        cc.OnCompleteFrame += MolSysEndFrameUpdate;

        cc.OnNewBondFrame += MolSysProcessBondFrameUpdate;
        cc.OnCompleteBondFrame += MolSysProcessBondFrameComplete;

        cc.OnNewName += MolSysProcessParticleNames;
        cc.OnCompleteNames += MolSysProcessNamesComplete;

        cc.OnHoomdStartup += MolSysPrepForNewHoomdSession;

        cc.setAllBondsRead(false);

        mBonds = new List<Vector3Int>();
        particleNames = new List<string>();

        // posOffset = new Vector3(0, 2.2f, 3.14f);

        last_graphics_update_time = 0.0f;
        last_graphics_update_frameCount = 0;
        total_fps_sum = 0.0f;
        num_graphics_updates = 0.0f;
    }

    private void MolSysProcessFrameUpdate(Frame frame)
    {
        //scaleF = 1.0f;
        //posOffset = new Vector3(0.0f, 0.0f, 0.0f);
        for (int i = frame.I; i < frame.I + frame.N; i++)
        {
            //frameUpdatePositions[i] = new Vector3(frame.Positions(i - frame.I).Value.Y * scaleF,
            //                                     frame.Positions(i - frame.I).Value.X * scaleF,
            //                                   frame.Positions(i - frame.I).Value.W * scaleF) + posOffset;
            frameUpdatePositions[i] = new Vector3(frame.Positions(i - frame.I).Value.X,
                                                  frame.Positions(i - frame.I).Value.Y,
                                                  frame.Positions(i - frame.I).Value.Z) * scaleF + posOffset;
            activeMolecules[i] = true;

        }
    }

    private void MolSysEndFrameUpdate()
    {
        num_graphics_updates += 1.0f;
        //update graphics
        for (int i = 0; i < num_positions_from_hoomd; i++)
        {
            moleculeTransforms[i].position = frameUpdatePositions[i];
            molTransforms_Sprites[i].position = frameUpdatePositions[i];

            if (max_particle_position.magnitude < frameUpdatePositions[i].magnitude)
            {
                max_particle_position = frameUpdatePositions[i];
                Debug.Log("max_particle_position mag: " + max_particle_position.magnitude);
            }

            if ((scaleF < 0.07f && i % 2 == 0) || !activeMolecules[i])
            {
                moleculeTransforms[i].gameObject.SetActive(false);
                molTransforms_Sprites[i].gameObject.SetActive(false);
            }
            else
            {
                moleculeTransforms[i].gameObject.SetActive(mesh_rend);
                molTransforms_Sprites[i].gameObject.SetActive(!mesh_rend);
            }
        }

        activeMolecules = new bool[num_positions_from_hoomd]; //reset active mol array.

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

    private void MolSysProcessBondFrameUpdate(string msg_string)
    {
        string[] _bonds = msg_string.Split('/');
        foreach (var b in _bonds)
        {
            string[] b_data = b.Split(',');
            int[] b_data_int = new int[bond_data_container_size];
            int idx = 0;
            foreach (var _d in b_data)
            {
                int tmp = 0;
                int.TryParse(_d, out tmp);
                b_data_int[idx] = tmp;
                idx++;
            }
            mBonds.Add(new Vector3Int(b_data_int[0], b_data_int[1], b_data_int[2]));
        }
    }

    private void MolSysProcessBondFrameComplete()
    {
        Debug.Log(mBonds.Count + " bonds read in total.");

        cc.setAllBondsRead(true);
        InitSystem();
    }

    private void MolSysProcessParticleNames(string msg_string)
    {
        string[] _names = msg_string.Split('/');
        foreach (string n in _names)
        {
            string[] name_data = n.Split(',');
            string pName = "";
            int idx = 0;
            foreach (var _d in name_data)
            {
                if (idx == 0)
                {
                    int tmp = 0;
                    int.TryParse(_d, out tmp);
                    if (tmp != num_names_read)
                    {
                        Debug.Log("Indexes out of Sync!" + "tmp: " + tmp + " nnr: " + num_names_read);
                    }
                }
                else
                {
                    pName = _d;
                }
                idx++;
            }
            particleNames.Add(pName);
            num_names_read++;
        }
    }

    private void MolSysProcessNamesComplete()
    {
        num_positions_from_hoomd = particleNames.Count;
        num_particles_from_hoomd = particleNames.Count;

    }

    private void MolSysPrepForNewHoomdSession()
    {
        Debug.Log("prep for new channel");
        particleNames.Clear();

        mBonds.Clear();
        cc.setAllBondsRead(false);

        num_names_read = 0;
        num_positions_from_hoomd = 0;
        num_particles_from_hoomd = 0;

    }

    private void InitSystem()
    {
        if (moleculeTransforms != null)
        {
            for (int i = 0; i < moleculeTransforms.Length; i++)
            {
                Destroy(moleculeTransforms[i].gameObject);
                Destroy(molTransforms_Sprites[i].gameObject);
            }
        }

        GameObject[] bonds_to_destroy = GameObject.FindGameObjectsWithTag("Bond");
        for (int i = 0; i < bonds_to_destroy.Length; i++)
        {
            Destroy(bonds_to_destroy[i]);
        }

        moleculeTransforms = new Transform[num_positions_from_hoomd];
        molTransforms_Sprites = new Transform[num_positions_from_hoomd];
        frameUpdatePositions = new Vector3[num_positions_from_hoomd];
        activeMolecules = new bool[num_positions_from_hoomd];

        if (num_particles_from_hoomd == 0)
        {
            Debug.Log("Trying to initialize molecule system without particle data being sent to Unity");
        }
        if (!cc.getAllBondsRead())
        {
            Debug.Log("Trying to initialize molecule system without all bond data being sent to Unity!");
        }
        //instantiate particles
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        for (int i = 0; i < num_particles_from_hoomd; i++)
        {

            moleculeTransforms[i] = Instantiate(atomPrefab);
            moleculeTransforms[i].position = transform.position;
            moleculeTransforms[i].SetParent(transform);

            molTransforms_Sprites[i] = Instantiate(atomPrefabSprite);
            molTransforms_Sprites[i].position = transform.position;
            molTransforms_Sprites[i].SetParent(transform);

            switch (particleNames[i])
            {
                case ("tip3p_H"):
                    if (i == 999)
                    {
                        Debug.Log(" part 999 name: " + particleNames[i] + "hy");
                    }
                    properties.SetColor("_Color", Color.gray);
                    break;

                case ("tip3p_O"):
                    {
                        if (i == 999)
                        {
                            Debug.Log(" part 999 name: " + particleNames[i] + " oxy ");
                        }
                        properties.SetColor("_Color", Color.red);
                        Vector3 default_prefab_scale = moleculeTransforms[i].localScale;
                        moleculeTransforms[i].localScale = default_prefab_scale * 2.0f;//Oxygen is ~twice as large as Hydrogen.
                        break;
                    }

                default:
                    {
                        Vector3 default_prefab_scale = moleculeTransforms[i].localScale;
                        moleculeTransforms[i].localScale = default_prefab_scale * 2.0f;
                        properties.SetColor("_Color", Color.red);
                        break;
                    }

            }

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
            molTransforms_Sprites[i].gameObject.SetActive(false);

        }
        //MaterialPropertyBlock bond_properties = new MaterialPropertyBlock();
        //instantiate bonds
        int c = 0;
        for (int i = 0; i < mBonds.Count; i++)
        {
            Vector3Int bond = mBonds[i];
            int a1 = bond.x;
            int a2 = bond.y;
            int type = bond.z;

            Transform bond_obj = Instantiate(bondPrefab);
            bond_obj.SetParent(transform);

            Bond _b = bond_obj.gameObject.GetComponent<Bond>();
            if (_b == null) { _b = bond_obj.gameObject.AddComponent<Bond>(); }
            //_b.msys = GetComponent<MoleculeSystemGPU>();

            _b.a1 = a1;
            _b.a2 = a2;
            _b.type = type;

            _b.atom1 = moleculeTransforms[a1].gameObject;
            _b.atom2 = moleculeTransforms[a2].gameObject;

            Vector3[] atomPositions = new Vector3[2];
            atomPositions[0] = _b.atom1.transform.position;
            atomPositions[1] = _b.atom2.transform.position;

            c++;

            properties.SetColor("_Color", Color.white);

            MeshRenderer r = bond_obj.GetComponent<MeshRenderer>();
            if (r)
            {
                r.SetPropertyBlock(properties);
            }
            else
            {
                for (int ci = 0; ci < bond_obj.childCount; ci++)
                {
                    r = bond_obj.GetChild(ci).GetComponent<MeshRenderer>();
                    if (r)
                    {
                        r.SetPropertyBlock(properties);
                    }
                }
            }

        }

        Debug.Log("num bonds pushed to graphics: " + c);
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

    public void setScaleF(float s)
    {
        scaleF = s;
    }

    public Vector3 getMaxParticlePos()
    {
        return max_particle_position;
    }

    public void InitSpriteMolView()
    {
        mesh_rend = false;
        if (molTransforms_Sprites.Length != moleculeTransforms.Length)
        {
            Debug.Log("sprite and mesh transforms not equal!");

        }

        for (int i = 0; i < molTransforms_Sprites.Length; i++)
        {
            molTransforms_Sprites[i].gameObject.SetActive(true);
            moleculeTransforms[i].gameObject.SetActive(false);
        }
    }

    public void InitMeshMolView()
    {
        mesh_rend = true;
        if (molTransforms_Sprites.Length != moleculeTransforms.Length)
        {
            Debug.Log("sprite and mesh transforms not equal!");

        }

        for (int i = 0; i < molTransforms_Sprites.Length; i++)
        {
            molTransforms_Sprites[i].gameObject.SetActive(false);
            moleculeTransforms[i].gameObject.SetActive(true);
        }
    }
}