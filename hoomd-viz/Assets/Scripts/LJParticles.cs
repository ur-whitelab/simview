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

    // Start is called before the first frame update
    void Start()
    {
        var cc = GameObject.Find("CommClient").GetComponent<CommClient>();
        cc.OnNewFrame += ProcessFrameUpdate;
    }

    private void ProcessFrameUpdate(Frame frame)
    {
        InitializeIfNeeded();

        // GetParticles is allocation free because we reuse the m_Particles buffer between updates
        int last_N = N;
        N = System.Math.Max(frame.N, N);
        m_System.GetParticles(m_Particles, N);

        for (int i = 0; i < N; i++)
        {
            if (i < frame.N)
            {
                m_Particles[i].remainingLifetime = 1;
                Debug.Log("Received new particle at " + frame.Positions(i).Value.X);
                m_Particles[i].position = new Vector3(frame.Positions(i).Value.X, frame.Positions(i).Value.W, frame.Positions(i).Value.Y);
            }
            else if (i < last_N)
            {
                m_Particles[i].remainingLifetime = 0;
            }
        }

        // Apply the particle changes to the Particle System
        m_System.SetParticles(m_Particles, N);
    }

    void InitializeIfNeeded()
    {
        if (m_System == null)
            m_System = GetComponent<ParticleSystem>();

        if (m_Particles == null || m_Particles.Length < m_System.main.maxParticles)
            m_Particles = new ParticleSystem.Particle[m_System.main.maxParticles];

        int max = m_System.main.maxParticles;
        m_System.GetParticles(m_Particles, max);

        for (int i = 0; i < N; i++)
        {
            m_Particles[i].startSize = m_System.main.startSize.constant;
            m_Particles[i].startColor = m_System.main.startColor.color;
        }

        // Apply the particle changes to the Particle System
        m_System.SetParticles(m_Particles, max);
    }
}
