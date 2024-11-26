using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(ClientNetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    public float speed = 10f;        // Movement speed multiplier
    public float maxSpeed = 5f;     // Maximum movement speed
    private Rigidbody rb;           // Reference to the Rigidbody
    private Camera playerCamera;    // Reference to the player's camera

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (!IsOwner)
        {
            rb.isKinematic = true; // Disable physics control for non-owned players
            return;
        }

        playerCamera = Camera.main; // Assumes the camera is the main camera
        rb.isKinematic = false;
        rb.freezeRotation = true; // Prevent rotation due to collisions
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            HandleMovement(); // Physics-based movement
            RotateToCameraDirection(); // Rotate to face the camera
        }
    }

    private void HandleMovement()
    {
        // Get input from the player
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        if (playerCamera == null || (moveX == 0 && moveZ == 0))
        {
            return; // No movement input
        }

        // Calculate movement direction based on the camera's orientation
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        forward.y = 0f; // Ignore vertical component for horizontal movement
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveZ + right * moveX).normalized;

        // Apply force in the direction of movement
        rb.AddForce(moveDirection * speed, ForceMode.Force);

        // Clamp the player's horizontal velocity to the max speed
        LimitSpeed();
    }

    private void RotateToCameraDirection()
    {
        if (playerCamera == null)
        {
            return;
        }

        // Rotate the player to align with the camera's forward direction
        Vector3 cameraForward = playerCamera.transform.forward;
        cameraForward.y = 0; // Ignore vertical rotation

        if (cameraForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            rb.MoveRotation(Quaternion.Lerp(rb.rotation, targetRotation, Time.deltaTime * 10f));
        }
    }

    private void LimitSpeed()
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            rb.velocity = new Vector3(horizontalVelocity.x, rb.velocity.y, horizontalVelocity.z);
        }
    }
}
