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
        if(Input.GetMouseButtonDown(isLeftOrRight)){
            if(isLeftOrRight == 0){
                animator.SetBool("LeftHand", true);
            }if(isLeftOrRight == 1){
                animator.SetBool("RightHand", true);
            }

            //FixedJoint fj = grabbedObj.AddComponent<FixedJoint>();
            //fj.connectedBody = rb;
            //fj.breakForce = 9000;

        }

        if(Input.GetMouseButtonUp(isLeftOrRight)){
            if(isLeftOrRight == 0){
                animator.SetBool("LeftHand", false);
            }if(isLeftOrRight == 1){
                animator.SetBool("RightHand", false);
            }

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
