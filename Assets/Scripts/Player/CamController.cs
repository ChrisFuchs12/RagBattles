using Unity.Netcode;
using UnityEngine;

public class CamController : NetworkBehaviour
{
    public Camera playerCamera;   // Camera assigned to the player
    public Transform root;        // Player's root object for rotation
    public ConfigurableJoint hipJoint, stomachJoint;

    public static float rotationSpeed = 30f;
    public float stomachOffset = 0f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    private void Start()
    {
        if (!IsOwner)
        {
            // Disable non-local player cameras
            playerCamera.enabled = false;
            playerCamera.GetComponent<AudioListener>().enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        HandleCameraRotation();
    }

    private void HandleCameraRotation()
    {
        rotationX += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
        rotationY -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
        rotationY = Mathf.Clamp(rotationY, -35, 60); // Limit vertical rotation

        // Apply rotation to the root transform
        Quaternion rootRotation = Quaternion.Euler(rotationY, rotationX, 0);
        root.rotation = rootRotation;

        // Adjust stomach joint rotation
        if (stomachJoint != null)
        {
            stomachJoint.targetRotation = Quaternion.Euler(rotationY + stomachOffset, 0, 0);
        }
    }
}
