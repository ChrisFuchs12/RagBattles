using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class DetectHit : NetworkBehaviour
{
    private Rigidbody rb;
    public float knockback = 100f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (!IsOwner)
        {
            rb.isKinematic = true; // Disable physics for non-owned players
            return;
        }

        rb.isKinematic = false;
    }

    private void OnCollisionEnter(Collision collision){
        if(collision.gameObject.tag == "Gun"){
            rb.AddForce(-rb.transform.forward * knockback, ForceMode.Force);
            Debug.Log("AAA");
        }
    }
}
