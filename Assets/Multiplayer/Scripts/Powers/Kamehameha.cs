using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Kamehameha : MonoBehaviour
{
    public GameObject circleBit;
    public GameObject beam;

    public Rigidbody rb;
    public float knockback = 1000f;

    private bool canFire = false;

    void Start()
    {
        
    }

    void Update()
    {
        if(Input.GetKey(KeyCode.F)){
            circleBit.SetActive(true);
            canFire = true;
        }

        if(Input.GetMouseButton(0) && canFire == true){
            beam.SetActive(true);
            rb.AddForce(-rb.transform.forward * knockback);
        }

        if(Input.GetMouseButtonUp(0)){
            circleBit.SetActive(false);
            beam.SetActive(false);
            canFire = false;
        }
    }
}
