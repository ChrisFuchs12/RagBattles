using Unity.Netcode;
using UnityEngine;

public class CamController : NetworkBehaviour
{
    public Camera playerCamera; // The camera to control
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
        // Disable the camera for non-owners
        if (!IsOwner)
        {
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(false);
            }
            return;
        }

        // Lock the cursor for the owning player
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void FixedUpdate()
    {
        if (IsOwner)
        {
            CamControll();
        }
    }

    void CamControll()
    {
        rotationX += Input.GetAxis("Mouse X") * rotationSpeed;
        rotationY -= Input.GetAxis("Mouse Y") * rotationSpeed;
        rotationY = Mathf.Clamp(rotationY, -60, 100);

        Quaternion rootRotation = Quaternion.Euler(rotationY, rotationX, 0);
        root.rotation = rootRotation;

        if (stomachJoint != null)
        {
            stomachJoint.targetRotation = Quaternion.Euler(rotationY + stomachOffset, 0, 0);
        }
    }
}

