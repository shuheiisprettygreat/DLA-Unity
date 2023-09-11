using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.VisualScripting;
using Random = UnityEngine.Random;

public class DLA : MonoBehaviour, IDLARenderable{

    public PointsOnSpline Spline;
    public ComputeShader Cs;

    // Number of RW particles
    [SerializeField] private NumParticle _numParticleEnum = NumParticle.NUM_4K;
    private int _nrActiveParticle;
    private int _nrParticle;
    
    
    // Buffers for particle and other Cs shits
    private const int THREAD_NUM = 256;
    private ComputeBuffer _particleBuffer;
    private ComputeBuffer _particleBufferWrite;

    // Bounds of RW
    private Vector3 _boundCenter = Vector3.zero;
    private float _boundRadius = 80f;

    private List<DirectedPoint> Seeds {
        get => Spline.DirectedPoints;
    }

    private void Start() {
        // Application.targetFrameRate = 1;
        
        _nrActiveParticle = (int)_numParticleEnum;
        
        // Setup particles.
        List<DirectedPoint> particles = new List<DirectedPoint>(Seeds);
        for (int i = 0; i < _nrActiveParticle; i++) {
            DirectedPoint p = new DirectedPoint() {
                position = Random.insideUnitSphere * _boundRadius + _boundCenter,
                tangent = new float3(0f, 0f, 0f),
                isActive = 1
            };
            particles.Add(p);
        }
        
        // Buffer particles
        _nrParticle = particles.Count;
        _particleBuffer = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(DirectedPoint)));
        _particleBuffer.SetData(particles);
        _particleBufferWrite = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(DirectedPoint)));
        _particleBufferWrite.SetData(new DirectedPoint[_nrParticle]);
    }

    void Update() {
        // Random walk
        int iRW = Cs.FindKernel("RW");
        Cs.SetVector("Seed", Random.insideUnitCircle * Random.Range(10f,80f));
        Cs.SetFloat("BoundRadius", _boundRadius);
        Cs.SetBuffer(iRW, "Particles", _particleBuffer);
        Cs.Dispatch(iRW, Mathf.CeilToInt((float)_nrParticle/THREAD_NUM), 1, 1);
        
        // Fix particle if bump into fixed ones.
        int iBump = Cs.FindKernel("FixIfBump");
        Cs.SetInt("NrParticle", _nrParticle);
        Cs.SetBuffer(iBump, "ParticlesRead", _particleBuffer);
        Cs.SetBuffer(iBump, "Particles", _particleBufferWrite);
        Cs.Dispatch(iBump, Mathf.CeilToInt((float)_nrParticle/THREAD_NUM), 1, 1);
        
        (_particleBuffer, _particleBufferWrite) = (_particleBufferWrite, _particleBuffer);

    }

    public int GetNrParticle() {
        return _nrParticle;
    }

    public ComputeBuffer GetParticleBuffer() {
        return _particleBuffer;
    }

    private void OnDrawGizmos() {
        if(Application.isPlaying) {
            Gizmos.color = Color.cyan;
            foreach (var seed in Seeds) {
                Gizmos.DrawSphere(seed.position, 0.5f);
                Gizmos.DrawLine(seed.position, seed.position+5*seed.tangent);
                }
        }
    }
}
