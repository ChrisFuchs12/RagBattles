using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketChair : MonoBehaviour
{
    public GameObject jetPack;
    public GameObject beam;

    public Rigidbody rb;
    public float knockback = 100f;

    private bool canFire = false;

    void Start()
    {
        
    }

    void Update()
    {
        if(Input.GetKey(KeyCode.L)){
            jetPack.SetActive(true);
            canFire = true;
        }

        if(Input.GetMouseButton(0) && canFire == true){
            beam.SetActive(true);
            rb.AddForce(new Vector3(0,knockback,0));
        }

        if(Input.GetMouseButtonUp(0)){
            jetPack.SetActive(false);
            beam.SetActive(false);
            canFire = false;
        }
    }
}
