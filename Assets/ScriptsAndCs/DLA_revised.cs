using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.VisualScripting;
using Random = UnityEngine.Random;

public class DLA_revised : MonoBehaviour, IDLARenderable{
    
    // moving particle and others will be separated in this implementation.
    public PointsOnSpline Spline;
    public ComputeShader Cs;
    
    // Parameters accessed with Inspector
    [SerializeField] private float stickDistance = 1;
    [SerializeField] private float particleSize = 1;
    [SerializeField] private float stickAngle = 0.7f;
    
    // Number of RW particles
    [SerializeField] private NumParticle _numParticleEnum = NumParticle.NUM_4K;
    private int _nrActiveParticle;
    private int _nrParticle;
    
    // Buffers for particle and other Cs shits
    private const int THREAD_NUM = 256;
    private ComputeBuffer _particleBufferRead;
    private ComputeBuffer _particleBufferWrite;

    // Bounds of RW
    [SerializeField] private Vector3 _boundCenter = Vector3.zero;
    [SerializeField] private float _boundRadius = 100f;

    public bool doUpdateDLA = true;
    
    ComputeBuffer countMovingRead;
    ComputeBuffer countMovingWrite;
    ComputeBuffer countFixedRead;
    ComputeBuffer countFixedWrite;

    private List<DirectedPoint> Seeds {
        get => Spline.DirectedPoints;
    }

    private void Start() {
        _nrActiveParticle = (int)_numParticleEnum;
        
        // Setup particles.
        List<DirectedPoint> particles = new List<DirectedPoint>(Seeds);
        for (int i = 0; i < _nrActiveParticle; i++) {
            DirectedPoint p = new DirectedPoint() {
                position = Random.insideUnitSphere * _boundRadius + _boundCenter,
                isActive = 1,
                life = 0
            };
            
            particles.Add(p);
        }
        
        // Buffer particles
        _nrParticle = particles.Count;
        _particleBufferRead = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(DirectedPoint)));
        _particleBufferRead.SetData(particles);
        _particleBufferWrite = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(DirectedPoint)));
        
        countMovingRead = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(int)));
        countMovingWrite = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(int)));
        countFixedRead = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(int)));
        countFixedWrite = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(int)));
        
        Cs.SetFloat("StickDistanceSq", stickDistance * stickDistance);
        Cs.SetFloat("StickAngle", stickAngle);
    }

    void Update() {
        if (Input.GetKeyUp(KeyCode.Space)) {
            doUpdateDLA = !doUpdateDLA;
        }

        if (!doUpdateDLA) return;
        
        Cs.SetInt("NrParticle", _nrParticle);
        
        //------------------------------------------------------------
        // Random walk
        int iRW = Cs.FindKernel("RW");
        Cs.SetVector("Seed", Random.insideUnitCircle * Random.Range(10f, 80f));
        Cs.SetFloat("BoundRadius", _boundRadius);
        Cs.SetVector("BoundOrigin", _boundCenter);
        Cs.SetBuffer(iRW, "ParticlesRead", _particleBufferRead);
        Cs.SetBuffer(iRW, "ParticlesWrite", _particleBufferWrite);
        Cs.Dispatch(iRW, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
        (_particleBufferRead, _particleBufferWrite) = (_particleBufferWrite, _particleBufferRead);

        //------------------------------------------------------------
        // Partition Array. Head side: fixed, Tail: moving
        int iStart = Cs.FindKernel("StartScan");
        int iScan = Cs.FindKernel("Scan");
        int iPack = Cs.FindKernel("Pack");

        // 1. Scan particles set 1 if particle is fixed.
        Cs.SetBuffer(iStart, "ParticlesRead", _particleBufferRead);
        // Elements of Buffer are independent. thus write to "Read" buffer to make my life easier.
        Cs.SetBuffer(iStart, "CountMovingWrite", countMovingRead);
        Cs.SetBuffer(iStart, "CountFixedWrite", countFixedRead);
        Cs.Dispatch(iStart, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);

        // 2. construct Prefix sums using 1's array.
        for (int i = 0; i < Mathf.CeilToInt(Mathf.Log(_nrParticle, 2)); i++) {
            Cs.SetBuffer(iScan, "CountMovingWrite", countMovingWrite);
            Cs.SetBuffer(iScan, "CountFixedWrite", countFixedWrite);
            Cs.SetBuffer(iScan, "CountMovingRead", countMovingRead);
            Cs.SetBuffer(iScan, "CountFixedRead", countFixedRead);
            Cs.SetInt("ShiftAmount", (int)Mathf.Pow(2, i));
            Cs.Dispatch(iScan, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
            (countMovingRead, countMovingWrite) = (countMovingWrite, countMovingRead);
            (countFixedRead, countFixedWrite) = (countFixedWrite, countFixedRead);
        }

        // 3. Reorder particles using prefix sums
        Cs.SetBuffer(iPack, "ParticlesRead", _particleBufferRead);
        Cs.SetBuffer(iPack, "ParticlesWrite", _particleBufferWrite);
        Cs.SetBuffer(iPack, "CountMovingRead", countMovingRead);
        Cs.SetBuffer(iPack, "CountFixedRead", countFixedRead);
        Cs.Dispatch(iPack, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
        (_particleBufferRead, _particleBufferWrite) = (_particleBufferWrite, _particleBufferRead);

        //------------------------------------------------------------
        //Fix particle if bump into fixed ones.
        int iBump = Cs.FindKernel("FixIfBump");
        Cs.SetBuffer(iBump, "ParticlesRead", _particleBufferRead);
        Cs.SetBuffer(iBump, "ParticlesWrite", _particleBufferWrite);
        Cs.Dispatch(iBump, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
        (_particleBufferRead, _particleBufferWrite) = (_particleBufferWrite, _particleBufferRead);
       
    }

    public int GetNrParticle() {
        return _nrParticle;
    }

    public ComputeBuffer GetParticleBuffer() {
        return _particleBufferRead;
    }

    private void OnDrawGizmos() {
        if(Application.isEditor) {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(_boundCenter, _boundRadius);
        }
    }
}
