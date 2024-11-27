using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(ClientNetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    public float speed = 10f;            // Movement speed multiplier for forward/backward
    public float strafeSpeed = 7.5f;    // Movement speed multiplier for strafing
    public float maxSpeed = 5f;         // Maximum movement speed
    public float jumpForce = 500f;  
    private Rigidbody rb;               // Reference to the Rigidbody
    private bool isGrounded = true;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (!IsOwner)
        {
            rb.isKinematic = true; // Disable physics for non-owned players
            return;
        }

        rb.isKinematic = false;
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            HandleMovement();
        }
    }

    private void HandleMovement()
    {
        // Check for input and apply corresponding forces
        if (Input.GetKey(KeyCode.W))
        {
            rb.AddForce(rb.transform.forward * speed * 1.5f, ForceMode.Force);
        }

        if (Input.GetKey(KeyCode.S))
        {
            rb.AddForce(-rb.transform.forward * speed, ForceMode.Force);
        }

        if (Input.GetKey(KeyCode.A))
        {
            rb.AddForce(-rb.transform.right * strafeSpeed, ForceMode.Force);
        }

        if (Input.GetKey(KeyCode.D))
        {
            rb.AddForce(rb.transform.right * strafeSpeed, ForceMode.Force);
        }


        // Clamp the player's velocity to the max speed
        LimitSpeed();
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
