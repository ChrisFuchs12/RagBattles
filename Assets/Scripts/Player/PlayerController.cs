using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(ClientNetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    public float speed = 5f;      // Movement speed
    public float jumpForce = 10f; // Jump force
    private Rigidbody rb;         // Reference to the Rigidbody
    private bool isGrounded;      // Check if the player is grounded

    public Transform groundCheck;  // Reference to the ground check (for detecting if grounded)
    public float groundDistance = 0.4f;  // Distance to check for ground
    public LayerMask groundMask;  // Layer mask to specify what is considered "ground"

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (!IsOwner)
        {
            // Disable Rigidbody and Input for non-owned players to ensure no interference
            rb.isKinematic = true; // Only the local player controls its physics
        }
    }

    private void Update()
    {
        if (IsOwner)  // Only allow the local player to control movement
        {
            HandleMovement();
            HandleJump();
        }
    }

    // Handle regular movement (WASD or Arrow keys)
    private void HandleMovement()
    {
        // Get player input for movement
        float moveX = Input.GetAxis("Horizontal");  // Horizontal input (A/D or Left/Right Arrow)
        float moveZ = Input.GetAxis("Vertical");    // Vertical input (W/S or Up/Down Arrow)

        // Calculate movement direction relative to player's orientation
        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized * speed * Time.deltaTime;

        // Apply movement locally
        rb.MovePosition(rb.position + move);
    }

    // Handle jumping (only when grounded)
    private void HandleJump()
    {
        // Check if the player is grounded before allowing jumping
        isGrounded = Physics.Raycast(groundCheck.position, Vector3.down, groundDistance, groundMask);

        if (isGrounded && Input.GetButtonDown("Jump"))  // When player presses jump (spacebar)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);  // Apply upward force
        }
    }
}
