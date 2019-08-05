using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatBuffers;
using HZMsg;
using UnityEngine.UI;
//using System.Threading.Tasks;
using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using AsyncIO;

public class MoleculeSystemGPU : MonoBehaviour
{
    [SerializeField]
    private Transform atomPrefab;
    [SerializeField]
    private Transform atomPrefabSprite;
    [SerializeField]
    private Transform bondPrefab;
    [SerializeField]
    private FilterChannelClient cc;
    [SerializeField]
    private InputField atomInputField;

    private Transform[] moleculeTransforms;
    private Vector3[] frameUpdatePositions;
    private bool[] activeMolecules;
    private bool[] badColor;
    private bool[] isolateMols;
    //private Vector3[] localAtomScales;

    private int num_positions_from_hoomd = 0;
    private int num_particles_from_hoomd = 0;
    private float init_radius = 50f;
    private float bond_length = 0.075f;

    [SerializeField]
    private Vector3 posOffset;

    [SerializeField]
    private float scaleF = 0.2f;

    [SerializeField]
    Text fps_test;

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

    private string atom_props_file_path = "oplsaa_key";


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

            if (max_particle_position.magnitude < frameUpdatePositions[i].magnitude)
            {
                max_particle_position = frameUpdatePositions[i];
            }

            if ((scaleF < 0.07f && i % 2 == 0) || !activeMolecules[i])
            {
                moleculeTransforms[i].gameObject.SetActive(false);
            }
            else
            {
                moleculeTransforms[i].gameObject.SetActive(isolateMols[i]);
            }

            if (badColor[i])
            {
                moleculeTransforms[i].gameObject.SetActive(false);
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

        fps_test.text = delta_fps.ToString();


        // (Mathf.Abs(delta_fps - avg_fps) >= 10.0f)
        //
        // Debug.Log("average fps: " + avg_fps);
        // Debug.Log("frames since last graphics update: " + frame_delta);
        //   Debug.Log("seconds since last graphics update: " + time_delta);
        //   Debug.Log("graphics fps: " + delta_fps);
        //

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
        Debug.Log("number of names from hoomd: " + particleNames.Count);
        num_positions_from_hoomd = particleNames.Count;
        num_particles_from_hoomd = particleNames.Count;

    }

    private void MolSysPrepForNewHoomdSession()
    {
        particleNames.Clear();

        mBonds.Clear();
        cc.setAllBondsRead(false);

        num_names_read = 0;
        num_positions_from_hoomd = 0;
        num_particles_from_hoomd = 0;

    }

    private Color stringToColor(string color_string)
    {
        switch (color_string)
        {
            case "black":
                return Color.black;
            case "red":
                return Color.red;
            case "white":
                return Color.white;
            case "blue":
                return Color.blue;
            default:
                Debug.Log("unexpected color string: " + color_string);
                return Color.clear;
        }
    }

    private void InitSystem()
    {
        // string filePath = Path.Combine(Application.streamingAssetsPath, atom_props_file_path);
        TextAsset f = Resources.Load(atom_props_file_path) as TextAsset;
        string filePath = f.ToString();

        //if (!File.Exists(filePath))
        //{
        //    Debug.Log("Could not find atom props dict at file path: " + atom_props_file_path + "; Will not be able to read atom properties!");
        //    return;
        //}

        /// string atomPropsAsJson = File.ReadAllText(filePath);
        var atom_prop_dict_values = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(filePath);

        if (moleculeTransforms != null)
        {
            for (int i = 0; i < moleculeTransforms.Length; i++)
            {
                Destroy(moleculeTransforms[i].gameObject);
            }
        }

        GameObject[] bonds_to_destroy = GameObject.FindGameObjectsWithTag("Bond");
        for (int i = 0; i < bonds_to_destroy.Length; i++)
        {
            Destroy(bonds_to_destroy[i]);
        }

        moleculeTransforms = new Transform[num_positions_from_hoomd];
        frameUpdatePositions = new Vector3[num_positions_from_hoomd];
        activeMolecules = new bool[num_positions_from_hoomd];
        isolateMols = new bool[num_positions_from_hoomd];
        badColor = new bool[num_positions_from_hoomd];
        //   localAtomScales = new Vector3[num_positions_from_hoomd];

        for (int i = 0; i < isolateMols.Length; i++)
        {
            isolateMols[i] = true;
        }


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

            // Debug.Log("pnames: " + particleNames[i]);
            Dictionary<string, object> small_dict = new Dictionary<string, object>();
            Color _color = Color.clear;
            double _radius = 1.0;
            string parsed_pnames_string = particleNames[i].Replace("\n", "");
            if (atom_prop_dict_values.TryGetValue(parsed_pnames_string, out small_dict))
            {
                // Debug.Log("color string: " + particleNames[i]);
                string color_string = (string)atom_prop_dict_values[parsed_pnames_string]["color"];
                _color = stringToColor(color_string);
                string _element = (string)atom_prop_dict_values[parsed_pnames_string]["element"];
                _radius = (double)atom_prop_dict_values[parsed_pnames_string]["radius"];
            }
            else
            {
                Debug.Log("name " + parsed_pnames_string + " not found.");
                //activeMolecules[i] = false;
                badColor[i] = true;

            }


            //float _radius = moleculeTransforms[i].localScale.x;
            //   bool tp = float.TryParse(radius_string, out _radius);
            //   Debug.Log("rad: " + _radius + "tp : " + tp);



            properties.SetColor("_Color", _color);
            Vector3 default_prefab_scale = moleculeTransforms[i].localScale;
            moleculeTransforms[i].localScale = default_prefab_scale * (float)_radius;

            //    localAtomScales[i] = moleculeTransforms[i].localScale;

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

    public void isolateAtoms()
    {
        string range_of_atoms = atomInputField.text;
        //inDebug.Log("roa: " + range_of_atoms);
        if (range_of_atoms == ".")
        {
            //enable all of them
            for (int i = 0; i < moleculeTransforms.Length; i++)
            {
                //moleculeTransforms[i].gameObject.transform.localScale = localAtomScales[i];
                moleculeTransforms[i].gameObject.SetActive(true);
                isolateMols[i] = true;
            }

        }
        else
        {
            string[] _atoms = range_of_atoms.Split('-');
            int _a_idx = 0;
            int a1_idx = 0;
            int a2_idx = 0;
            bool a1_parse = false;
            bool a2_parse = false;
            foreach (string a in _atoms)
            {
                if (_a_idx == 0)
                {
                    a1_parse = int.TryParse(a, out a1_idx);

                }
                else
                {
                    a2_parse = int.TryParse(a, out a2_idx);
                }
                _a_idx++;
            }
            //should just be two
            if (_atoms.Length != 2)
            {
                Debug.Log("range of atoms length: " + range_of_atoms.Length);
            }



            if (a1_parse && a2_parse)
            {
                int max_idx = Mathf.Max(a1_idx, a2_idx);
                if (max_idx >= moleculeTransforms.Length)
                {
                    return;
                }
                int min_idx = 0;
                if (a1_idx == max_idx)
                {
                    min_idx = a2_idx;
                }
                else
                {
                    min_idx = a1_idx;
                }

                for (int i = 0; i < moleculeTransforms.Length; i++)
                {
                    if (i >= min_idx && i <= max_idx)
                    {
                        isolateMols[i] = true;
                        // moleculeTransforms[i].localScale = localAtomScales[i] * 4;
                        // moleculeTransforms[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        isolateMols[i] = false;
                        // moleculeTransforms[i].gameObject.SetActive(false);
                    }
                }


            }
            else
            {
                Debug.Log("couldn't parse inputs");
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

    public void ToggleMeshView(bool _mrend)
    {
        mesh_rend = _mrend;
        for (int i = 0; i < moleculeTransforms.Length; i++)
        {
            moleculeTransforms[i].gameObject.SetActive(mesh_rend);
        }
    }

}