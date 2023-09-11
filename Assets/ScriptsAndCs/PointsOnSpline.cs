using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using static Unity.Mathematics.math;

[RequireComponent(typeof(SplineContainer))]
public class PointsOnSpline : MonoBehaviour {

    public List<DirectedPoint> DirectedPoints;

    public int NrPoint;
    
    [SerializeField] private SplineContainer _spline;

    void Start() {
        DirectedPoints = new List<DirectedPoint>();
        for (float p = 0f; p <= 1f; p += 1f / NrPoint) {
            _spline.Evaluate(p, out var position, out var tangent, out _);
            DirectedPoints.Add(
                new DirectedPoint() {
                    position=position, 
                    tangent=normalizesafe(tangent, 0),
                    acceleration = _spline.EvaluateAcceleration(p) + float3(2,0,0),
                    isActive = 0,
                    life = 0
                }
            );
        }
    }

    void OnValidate() {
        Start();
    }

    private void OnDrawGizmos() {
        if (Application.isEditor) {
            foreach (var p in DirectedPoints) {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(p.position, 0.5f);
                Gizmos.DrawLine(p.position, p.position+5*p.tangent);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(p.position, p.position+0.05f*p.acceleration);
            }
        }
    }
}
