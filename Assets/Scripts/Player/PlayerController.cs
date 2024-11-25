using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public float speed = 5f;      // Movement speed
    public float jumpForce = 10f; // Jump force
    private Rigidbody rb;         // Regular Rigidbody reference
    private bool isGrounded;      // Check if the player is grounded

    public Transform groundCheck;  // Reference to the ground check (for detecting if grounded)
    public float groundDistance = 0.4f;  // Distance to check for ground
    public LayerMask groundMask;  // Layer mask to specify what is considered "ground"

    private void Start()
    {
        rb = GetComponent<Rigidbody>();  // Get the Rigidbody component
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

        // Apply movement on the server
        MovePlayerServerRpc(move);
    }

    // Handle jumping (only when grounded)
    private void HandleJump()
    {
        // Check if the player is grounded before allowing jumping
        isGrounded = Physics.Raycast(groundCheck.position, Vector3.down, groundDistance, groundMask);

        if (isGrounded && Input.GetButtonDown("Jump"))  // When player presses jump (spacebar)
        {
            JumpServerRpc();
        }
    }

    // ServerRpc for movement
    [ServerRpc]
    private void MovePlayerServerRpc(Vector3 moveDirection, ServerRpcParams rpcParams = default)
    {
        // Verify the owner is making the move request
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (clientId != OwnerClientId)
            return;

        // Update position on the server
        rb.MovePosition(rb.position + moveDirection);

        // Broadcast position change to all clients
        UpdatePlayerPositionClientRpc(rb.position);
    }

    // ServerRpc for jumping
    [ServerRpc]
    private void JumpServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (clientId != OwnerClientId)
            return;

        if (rb != null)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);  // Apply upward force
        }
    }

    // ClientRpc to update player position for all clients
    [ClientRpc]
    private void UpdatePlayerPositionClientRpc(Vector3 newPosition)
    {
        if (!IsOwner) // Prevent overriding local player position
        {
            rb.position = newPosition;
        }
    }
}
