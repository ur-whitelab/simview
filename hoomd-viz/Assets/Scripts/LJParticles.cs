using HZMsg;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class LJParticles : MonoBehaviour
{

    ParticleSystem m_System;
    ParticleSystem.Particle[] m_Particles;
    private int N = 0;
    private Vector3 box;

    // MoleculeSystem moleculeSystem;

    //[SerializeField]
    //HBallManager hbm;

    bool init_needed = true;
    // Start is called before the first frame update
    void Start()
    {
        var cc = GameObject.Find("CommClient").GetComponent<CommClient>();
        //moleculeSystem = GameObject.Find("MoleculeSystem").GetComponent<MoleculeSystem>();
        cc.OnNewFrame += ProcessFrameUpdate;
        cc.OnCompleteFrame += EndFrameUpdate;
        cc.OnSimulationUpdate += updateInterface;

    }

    private void updateInterface(Dictionary<string, string> data)
    {
        // get box size
        //float sf = 0.01f;
        box = new Vector3(float.Parse(data["lx"]), float.Parse(data["ly"]), float.Parse(data["lz"]));

      //  Debug.Log("box: " + box);
    }

    private void ProcessFrameUpdate(Frame frame)
    {
        InitializeIfNeeded();

        // GetParticles is allocation free because we reuse the m_Particles buffer between updates
        m_System.GetParticles(m_Particles, N);

     //   List<Vector4> mol_positions = new List<Vector4>();

        for (int i = frame.I; i < frame.I + frame.N; i++)
        {
            m_Particles[i].remainingLifetime = 10;
            m_Particles[i].startSize = 2;
            //Debug.Log("particles zpos: " + frame.Positions(i - frame.I).Value.Y);
            m_Particles[i].position = new Vector3(frame.Positions(i - frame.I).Value.X, frame.Positions(i - frame.I).Value.W, frame.Positions(i - frame.I).Value.Y);
            
        //    mol_positions.Add(new Vector4(frame.Positions(i - frame.I).Value.X, frame.Positions(i - frame.I).Value.W, frame.Positions(i - frame.I).Value.Y, frame.Positions(i - frame.I).Value.Z));
        }
        //atomParent.updatePositions(mol_positions);
        //  moleculeSystem.updateSystem(particle_positions);

        // hbm.updateMolSystem(mol_positions);
        //Debug.Log("mp length: " + m_Particles.Length);
        m_System.SetParticles(m_Particles, N);
    }

    private void EndFrameUpdate()
    {
        // Apply the particle changes to the Particle System
        m_System.SetParticles(m_Particles, N);
    }

    void InitializeIfNeeded()
    {
        if (m_System == null)
            m_System = GetComponent<ParticleSystem>();

        if (init_needed)
        {
            m_Particles = new ParticleSystem.Particle[m_System.main.maxParticles];

            int max = m_System.main.maxParticles;
            N = 9126;
        //    Debug.Log("max: " + N);
            m_System.GetParticles(m_Particles, max);

            List<Vector4> init_particle_positions = new List<Vector4>();
            List<float> init_particle_sizes = new List<float>();
            List<Vector2Int> init_bonds = new List<Vector2Int>();
            for (int i = 0; i < N; i++)
            {
                m_Particles[i].startSize = 0;
                m_Particles[i].startColor = m_System.main.startColor.color;
                m_Particles[i].remainingLifetime = 0;

                //preparing data for moleculeSystem

                init_particle_positions.Add(Vector4.one);
                init_particle_sizes.Add(0.01f);

            }
            // Apply the particle changes to the Particle System
            m_System.SetParticles(m_Particles, max);

           // moleculeSystem.addParticles(init_particle_positions, init_particle_sizes);

            init_needed = false;
        }
    }



}
