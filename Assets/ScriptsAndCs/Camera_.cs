using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Camera_ : MonoBehaviour {

    public GameObject dlaObject;

    private float startTime;

    void Start() {
        transform.position = new Vector3(0, 0, -260);
    }
    
    void Update() {
        bool isDlaEnabled = dlaObject.GetComponent<DLA_revised>().doUpdateDLA;
        if (isDlaEnabled) {
            
        }
        else {
            transform.RotateAround(Vector3.zero, Vector3.up, 0 * Time.deltaTime);
            transform.LookAt(Vector3.zero, Vector3.up);
        }
        
    }
}
