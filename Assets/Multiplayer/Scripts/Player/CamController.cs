using Unity.Netcode;
using UnityEngine;

public class CamController : NetworkBehaviour
{
    [Header("Camera")]
    public Camera playerCamera;
    public GameObject balancer;

    [Header("Rotation Settings")]
    public static float rotationSpeed = 30f;
    [HideInInspector] public float rotationX = 0f;
    [HideInInspector] public float rotationY = 0f;

    [Header("Rig")]
    public Transform root;
    public float stomachOffset;
    public ConfigurableJoint hipJoint, stomachJoint;

    void Start()
    {
        if (!IsOwner)
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        CamControll();
    }

    void CamControll()
    {
        // Accumulate mouse input — shared with ScopeSystem for the transition
        rotationX += Input.GetAxis("Mouse X") * rotationSpeed * Time.fixedDeltaTime * 50f;
        rotationY -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.fixedDeltaTime * 50f;
        rotationY  = Mathf.Clamp(rotationY, -60f, 100f);

        // Always drive the root rotation — same in both third and first person
        root.rotation = Quaternion.Euler(rotationY, rotationX, 0f);

        if (stomachJoint != null)
            stomachJoint.targetRotation = Quaternion.Euler(rotationY + stomachOffset, 0f, 0f);
    }
}