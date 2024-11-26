using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(ClientNetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    public float speed = 10f;        // Movement speed multiplier
    public float maxSpeed = 5f;     // Maximum movement speed
    private Rigidbody rb;           // Reference to the Rigidbody

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (!IsOwner)
        {
            rb.isKinematic = true; // Disable physics control for non-owned players
            return;
        }

        // Ensure Rigidbody settings are correct
        rb.isKinematic = false;
        rb.freezeRotation = true; // Prevent rotation due to collisions
    }

    private void Update()
    {
        if (IsOwner)  // Only allow the local player to control movement
        {
            HandleMovement();
        }
    }

    // Handle physics-based movement
    private void HandleMovement()
    {
        // Get player input for movement
        float moveX = Input.GetAxis("Horizontal");  // Horizontal input (A/D or Left/Right Arrow)
        float moveZ = Input.GetAxis("Vertical");    // Vertical input (W/S or Up/Down Arrow)

        // Calculate movement direction relative to player's orientation
        Vector3 moveDirection = (transform.right * moveX + transform.forward * moveZ).normalized;

        // Apply force in the movement direction
        rb.AddForce(moveDirection * speed, ForceMode.Force);

        // Limit maximum speed
        LimitSpeed();
    }

    // Clamp the player's speed to a maximum value
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
