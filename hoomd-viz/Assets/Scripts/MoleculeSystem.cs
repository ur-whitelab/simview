using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MoleculeSystem : MonoBehaviour {

    //This is the list of all particle x, y, z positions, and
    // includes their atom type as the w entry in a Vector4
    private List<Vector4> particlePositionsList;
    //This is the list of tuples of bonded atom indices
    // e.g. [(0,1), (1,2), (2,3)] for H2O
    private List<Vector2Int> bondsList;

    //this is the list of sphere gameobjects that we render at particle positions
    private List<GameObject> sphereList;
    //this is the list of cylinder gameobjects that we render at bond locations
    private List<GameObject> cylinderList;

    // Use this for initialization
    void Start () {
        particlePositionsList = new List<Vector4>();
        bondsList = new List<Vector2Int>();
        sphereList = new List<GameObject>();
        cylinderList = new List<GameObject>();
    }

    //fill up our list of particle positions, and do initial render
    //MUST call first before addBonds, and only call ONCE
    void addParticles(List<Vector4> particlesPositions, List<float> particlesSizes){
        int i = 0;
        foreach(Vector4 particlePos in particlesPositions){
            //track its position -- need this because we need w, but
            // w is not taking its usual role
            particlePositionsList.Add(particlePos);
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = new Vector3(particlePos.x,particlePos.y,particlePos.z);// discard atom type
            //get size of sphere to be rendered
            float scale = particlesSizes[i];
            //apply the scaling
            sphere.transform.localScale = new Vector3(scale, scale, scale);
            //track this sphere object
            sphereList.Add(sphere);
            i++;
        }
    }

    //fill up our list of bonds positions, and only call ONCE
    void addBonds(List<Vector2Int> bondsList){
        foreach(Vector2Int bond in bondsList){
            bondsList.Add(bond);
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            //Put the cylinder between the two spheres it corresponds to
            cylinder.transform.position = Vector3.Lerp(sphereList[bond[0]].transform.position,
                sphereList[bond[1]].transform.position,
                0.5f);
            //point the cylinder toward one of the two
            // since it's already centered between them it aligns
            cylinder.transform.LookAt(sphereList[bond[0]].transform);
            //Now scale the cylinder lenghtwise by the distance between spheres
            Vector3 temp = cylinder.transform.localScale;
            temp.z = Vector3.Distance(sphereList[bond[0]].transform.position,
                sphereList[bond[1]].transform.position);
            cylinder.transform.localScale = temp;
            cylinderList.Add(cylinder);
        }
    }

    //update the coordinates of all the particles and bonds in the system
    void updateSystem(List<Vector4> particlesPositions){
        int i = 0;
        foreach(Vector4 particlePos in particlesPositions){
            sphereList[i].transform.position = new Vector3(particlePos.x,particlePos.y,particlePos.z);// discard atom type
            i++;
        }
        i = 0;
        foreach(Vector2Int bond in bondsList){
            //Put the cylinder between the two spheres it corresponds to            
            cylinderList[i].transform.position = Vector3.Lerp(sphereList[bond[0]].transform.position,
                sphereList[bond[1]].transform.position,
                0.5f);
            //point the cylinder toward one of the two
            // since it's already centered between them it aligns
            cylinderList[i].transform.LookAt(sphereList[bond[0]].transform);
            //Now scale the cylinder lenghtwise by the distance between spheres
            Vector3 temp = cylinderList[i].transform.localScale;
            temp.z = Vector3.Distance(sphereList[bond[0]].transform.position,
                sphereList[bond[1]].transform.position);
            cylinderList[i].transform.localScale = temp;
            i++;
        }
    }
    
    // Update is called once per frame
    void Update () {
    //TODO: put a test method here for like "getkeydown" to press space and spawn a system.
    //TODO: make one of the particles controllable to test the bonds moving with it.
    }
}
