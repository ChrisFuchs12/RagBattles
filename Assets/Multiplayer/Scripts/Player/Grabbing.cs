using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grabbing : MonoBehaviour
{
    public Rigidbody rb;
    public Animator animator;
    private GameObject grabbedObj;
    private bool alreadyGrabbing = false;
    public int isLeftOrRight;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Q)){
            animator.SetBool("LeftHand", true);
        }
        if(Input.GetKeyDown(KeyCode.E)){
            animator.SetBool("RightHand", true);
        }

        if(Input.GetKeyUp(KeyCode.Q)){
            animator.SetBool("LeftHand", false);

            if(grabbedObj != null){
            Destroy(grabbedObj.GetComponent<FixedJoint>());
            }

            grabbedObj = null;
        }
        if(Input.GetKeyUp(KeyCode.E)){
            animator.SetBool("RightHand", false);

            if(grabbedObj != null){
            Destroy(grabbedObj.GetComponent<FixedJoint>());
            }

            grabbedObj = null;
        
        }
    }



    private void OnColliderEnter(Collider other){
        grabbedObj = other.gameObject;
    }

    private void OnTriggerExit(Collider other){
        grabbedObj = null;
    }
}
