using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamController : MonoBehaviour
{

    public Camera playerCamera;
    public GameObject balancer;
    public static float rotationSpeed = 30;

    private float rotationX = 0;
    private float rotationY = 0;
    public Transform root;

    public float stomachOffset;
    public ConfigurableJoint hipJoint, stomachJoint;

    private bool canMove = true;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void FixedUpdate()
    {
       CamControll();
    }

    void CamControll(){
        rotationX += Input.GetAxis("Mouse X")*rotationSpeed;
        rotationY -= Input.GetAxis("Mouse Y")*rotationSpeed;
        rotationY = Mathf.Clamp(rotationY, -35, 60);

        Quaternion rootRotation = Quaternion.Euler(rotationY, rotationX, 0);
        root.rotation = rootRotation;
        
        stomachJoint.targetRotation = Quaternion.Euler(rotationY + stomachOffset, 0, 0);
    }
}