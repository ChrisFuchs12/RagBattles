using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class controllAnimator : MonoBehaviour
{
    public Animator animator;

    void Start()
    {
        
    }

    
    void Update()
    {
        if(Input.GetKey("w")){
            animator.SetBool("Walking", true);
        }if(Input.GetKeyUp("w")){
            animator.SetBool("Walking", false);
        }

        if(Input.GetMouseButtonDown(1)){
            animator.SetBool("RightHand", true);
        }if(Input.GetMouseButtonUp(1)){
            animator.SetBool("RightHand", false);
        }
    }
}
