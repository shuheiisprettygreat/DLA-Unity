using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using UnityEngine.Rendering;

public class RenderParticles : MonoBehaviour {
    [SerializeField] private Material mat;
    [SerializeField] private Mesh mesh;
    [SerializeField] private GameObject camera;
    [SerializeField] private GameObject dlaObject;
    
    private IDLARenderable dla;

    private CommandBuffer commandBuffer;
    
    private void Start() {
        dla = dlaObject.GetComponent<IDLARenderable>();
        commandBuffer = new CommandBuffer();
        commandBuffer.name = "dla instancing";
        camera.GetComponent<Camera>().AddCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);
    }

    private void Update() {
        int n = dla.GetNrParticle();
        ComputeBuffer buffer = dla.GetParticleBuffer();
        mat.SetBuffer("particles", buffer);

        Matrix4x4 m = dlaObject.transform.localToWorldMatrix;
        var rotation = Matrix4x4.Rotate(dlaObject.transform.rotation);
        
        mat.SetMatrix("ParentTransform", m*rotation);

        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 200);
        
        //Graphics.DrawMeshInstancedProcedural(mesh, 0, mat, bounds, n);
        
        // command buffer version
        commandBuffer.Clear();
        commandBuffer.DrawMeshInstancedProcedural(mesh, 0, mat, 0, n);
    }
    
    
}
