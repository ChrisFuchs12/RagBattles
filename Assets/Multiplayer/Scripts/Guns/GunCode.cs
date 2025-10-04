using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GunCode : NetworkBehaviour
{
    // Assigning variables
    [SerializeField] private Transform firingPoint;
    [SerializeField] private GameObject bullet;

    [SerializeField] private GameObject slide;

    // Settings
    [SerializeField] private float bulletSpeed = 50;
    [SerializeField] private float ammo = 50;
    [SerializeField] private float maxAmmo = 50;

    void Update()
    {
        // Allow everyone (host and clients) to shoot
        if (Input.GetMouseButtonDown(0) && IsOwner) // Ensure only local player can send RPCs
        {
            
            RequestSpawnBulletServerRpc();
        }
    }

    [ServerRpc]
    private void RequestSpawnBulletServerRpc(ServerRpcParams rpcParams = default)
    {
        

        // Instantiate and handle bullet on the server
        GameObject spawnedObj = Instantiate(bullet, firingPoint.position, firingPoint.rotation);
        
        // Check for a NetworkObject component
        NetworkObject networkObject = spawnedObj.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            
            Destroy(spawnedObj);
            return;
        }

        // Spawn the bullet on the network
        networkObject.Spawn();

        // Apply force on the server
        Rigidbody rb = spawnedObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            
            rb.AddForce(firingPoint.forward * bulletSpeed, ForceMode.Impulse);
        }
        else
        {
            Debug.LogError("No Rigidbody found on the bullet prefab!");
        }
    }
}

