using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class DLA_GridSort : MonoBehaviour, IDLARenderable
{
    // Spline and ComputeShader
    public PointsOnSpline Spline;
    public ComputeShader Cs;
    
    // Parameters of DLA
    [SerializeField] private float stickDistance = 1.3f;
    [SerializeField] private float particleSize = 5f;
    [SerializeField] private float stickAngle = 0.7f;

    [SerializeField] private Vector3 externalForce = new Vector3(0, 0, 0);
    
    // Number of RW particles
    [SerializeField] private NumParticle numParticleEnum = NumParticle.NUM_4K;
    private int _nrActiveParticle;
    private int _nrParticle;
    
    // Buffers for particle and other Cs shits
    private const int THREAD_NUM = 256;
    private ComputeBuffer _particleBufferRead;
    private ComputeBuffer _particleBufferWrite;

    // Bounds of RW
    [SerializeField] private Vector3 boundCenter = Vector3.zero;
    [SerializeField] private float boundRadius = 100f;
    
    // Parameters for Grid Sort
    [SerializeField] private int SizeOfHash = 5000;
    private float areaSize;         // size of grid-managed cube. 
    private Vector3 gridOrigin;    // Coordinate where (0,0) grid located. -gridOrigin is opposite end.
    private float gridSize;         // size of Grid
    private int nrGridOnEdge;
    private ComputeBuffer _hashedParticles;
    private ComputeBuffer _gridIndices;
    
    #region Structs for spatial hashing and neighbor search
    public struct HashedParticle
    {
        public int iParticle;
        public int iHash;
    };

    public struct GridIndex
    {
        public int start;
        public int end;
    };
    #endregion

    public bool doUpdateDLA = true;

    private List<DirectedPoint> Seeds {
        get => Spline.DirectedPoints;
    }

    //----------------------------------------------------------------
    private void Start() {
        
        Application.targetFrameRate = 30;
        _nrParticle = (int)numParticleEnum;

        // Setup particles.
        List<DirectedPoint> particles = new List<DirectedPoint>(Seeds);
        for (int i = 0; i < particles.Count; i++) {
            var directedPoint = particles[i];
            directedPoint.acceleration += (float3)externalForce;
            particles[i] = directedPoint;
        }
        
        for (int i = 0; i < _nrParticle - Seeds.Count; i++) {
            DirectedPoint p = new DirectedPoint() {
                position = Random.insideUnitSphere * boundRadius + boundCenter,
                isActive = 1,
                life = 0
            };
            particles.Add(p);
        }
        
        Debug.Log(_nrParticle == particles.Count);
        
        // Setup grid-related values
        areaSize = boundRadius * 2;
        gridOrigin = boundCenter - Vector3.one * areaSize*0.5f;
        gridSize = Mathf.Max(5, stickDistance); // Grid must be larger than sensing radius.
        nrGridOnEdge = Mathf.CeilToInt(areaSize / gridSize); // Total #Grid is nrGridOnEdge^3
        _hashedParticles = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(HashedParticle)));
        _gridIndices = new ComputeBuffer(SizeOfHash, Marshal.SizeOf(typeof(GridIndex)));
        Debug.Log($"{areaSize},{gridOrigin},{gridSize},{nrGridOnEdge}");

        // Buffer particles
        _particleBufferRead = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(DirectedPoint)));
        _particleBufferRead.SetData(particles);
        _particleBufferWrite = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(DirectedPoint)));
        
        // Set to Cs
        Cs.SetFloat("StickDistanceSq", stickDistance * stickDistance);
        Cs.SetFloat("StickAngle", stickAngle);
        Cs.SetFloat("BoundRadius", boundRadius);
        Cs.SetVector("BoundOrigin", boundCenter);
        Cs.SetVector("GridOrigin", gridOrigin);
        Cs.SetFloat("GridSize", gridSize);
        Cs.SetInt("NrGridOnEdge", nrGridOnEdge);
        Cs.SetInt("SizeOfHash", SizeOfHash);
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
        Cs.SetBuffer(iRW, "ParticlesRead", _particleBufferRead);
        Cs.SetBuffer(iRW, "ParticlesWrite", _particleBufferWrite);
        Cs.Dispatch(iRW, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
        (_particleBufferRead, _particleBufferWrite) = (_particleBufferWrite, _particleBufferRead);
        
        //------------------------------------------------------------
        // Compute particles hash.
        int iCalcHash = Cs.FindKernel("CalcParticleHash");
        Cs.SetBuffer(iCalcHash, "ParticlesRead", _particleBufferRead);
        Cs.SetBuffer(iCalcHash, "HashedParticlesWrite", _hashedParticles);
        Cs.Dispatch(iCalcHash, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);

        // Bitonic Sort
        int iSort = Cs.FindKernel("BitonicSort");
        int exponent = (int)Mathf.Log(_nrParticle, 2);
        
        ComputeBuffer hashedTemp = new ComputeBuffer(_nrParticle, Marshal.SizeOf(typeof(HashedParticle)));
        for (int stage = 1; stage <= exponent; stage++) {
            Cs.SetInt("Stage", stage);
            Cs.SetInt("Step", 2 << stage - 1);
            for (int offsetExp=stage-1; offsetExp>=0; offsetExp--) {
                int offset = 1 << offsetExp;
                Cs.SetInt("OffsetExp", offsetExp);
                Cs.SetInt("Offset", offset);
                Cs.SetBuffer(iSort, "HashedParticlesRead", _hashedParticles);
                Cs.SetBuffer(iSort, "HashedParticlesWrite", hashedTemp);
                Cs.Dispatch(iSort, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
                (_hashedParticles, hashedTemp) = (hashedTemp, _hashedParticles);
            }
        }
        hashedTemp.Dispose();
        
        // Clear Grid Indices
        int iClearIndices = Cs.FindKernel("ClearGridIndices");
        Cs.SetBuffer(iClearIndices, "GridIndices", _gridIndices);
        Cs.Dispatch(iClearIndices, Mathf.CeilToInt((float)SizeOfHash / THREAD_NUM), 1, 1);
        
        // Build Grid Indices
        int iBuildIndices = Cs.FindKernel("BuildGridIndices");
        Cs.SetBuffer(iBuildIndices, "ParticlesRead", _particleBufferRead);
        Cs.SetBuffer(iBuildIndices, "GridIndices", _gridIndices);
        Cs.SetBuffer(iBuildIndices, "HashedParticlesRead", _hashedParticles);
        Cs.Dispatch(iBuildIndices, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
        
        
        //------------------------------------------------------------
        //Fix particle if bump into fixed ones.
        int iBump = Cs.FindKernel("FixIfBump");
        Cs.SetBuffer(iBump, "ParticlesRead", _particleBufferRead);
        Cs.SetBuffer(iBump, "ParticlesWrite", _particleBufferWrite);
        Cs.SetBuffer(iBump, "GridIndices", _gridIndices);
        Cs.SetBuffer(iBump, "HashedParticlesRead", _hashedParticles);
        Cs.Dispatch(iBump, Mathf.CeilToInt((float)_nrParticle / THREAD_NUM), 1, 1);
        (_particleBufferRead, _particleBufferWrite) = (_particleBufferWrite, _particleBufferRead);

        //Debug
        // HashedParticle[] arr = new HashedParticle[_nrParticle];
        // GridIndex[] arr2 = new GridIndex[SizeOfHash];
        // _hashedParticles.GetData(arr);
        // _gridIndices.GetData(arr2);
        //
        // List<int> existingGrid = new List<int>();
        // Debug.Log("----------------");
        // for (int i = 200; i < 210; i++) {
        //     existingGrid.Add(arr[i].iHash);
        //     Debug.Log($"{i}: {arr[i].iParticle},{arr[i].iHash}");
        // }
        //
        // foreach (var i in existingGrid) {
        //     Debug.Log($"{i}:{arr2[i].start},{arr2[i].end}");
        // }

        // int sum = 0;
        // foreach(GridIndex gi in arr2) {
        //     sum += gi.end - gi.start;
        // }
        //
        // Debug.Log(sum);

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
            Gizmos.DrawWireSphere(boundCenter, boundRadius);
        }
    }
}
