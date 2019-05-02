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

    // Start is called before the first frame update
    void Start()
    {
        var cc = GameObject.Find("CommClient").GetComponent<CommClient>();
        cc.OnNewFrame += ProcessFrameUpdate;
        cc.OnCompleteFrame += EndFrameUpdate;
        cc.OnSimulationUpdate += updateInterface;
    }

    private void updateInterface(Dictionary<string, string> data) {
        // get box size
        box = new Vector3(float.Parse(data["lx"]), float.Parse(data["ly"]), float.Parse(data["lz"]));
    }

    private void ProcessFrameUpdate(Frame frame)
    {
        InitializeIfNeeded();

        // GetParticles is allocation free because we reuse the m_Particles buffer between updates
        m_System.GetParticles(m_Particles, N);


        for (int i = frame.I; i < frame.I + frame.N; i++)
        {
                m_Particles[i].remainingLifetime = 10;
                m_Particles[i].startSize = 2;
                m_Particles[i].position = new Vector3(frame.Positions(i - frame.I).Value.X, frame.Positions(i - frame.I).Value.W, frame.Positions(i- frame.I).Value.Y);
        }
        m_System.SetParticles(m_Particles, N);
    }

    private void EndFrameUpdate() {
        // Apply the particle changes to the Particle System
        m_System.SetParticles(m_Particles, N);
    }

    void InitializeIfNeeded()
    {
        if (m_System == null)
            m_System = GetComponent<ParticleSystem>();

        if (m_Particles == null || m_Particles.Length < m_System.main.maxParticles) {
            m_Particles = new ParticleSystem.Particle[m_System.main.maxParticles];

            int max = m_System.main.maxParticles;
            N = max;
            m_System.GetParticles(m_Particles, max);

            for (int i = 0; i < N; i++)
            {
                m_Particles[i].startSize = 0;
                m_Particles[i].startColor = m_System.main.startColor.color;
                m_Particles[i].remainingLifetime = 0;
            }

            // Apply the particle changes to the Particle System
            m_System.SetParticles(m_Particles, max);
        }
    }
}
