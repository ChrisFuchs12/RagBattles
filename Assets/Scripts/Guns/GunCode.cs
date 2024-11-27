using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GunCode : NetworkBehaviour
{
    //assigning variables
    [SerializeField] private Transform firingPoint;
    [SerializeField] private GameObject bullet;

    //settings
    [SerializeField] private float bulletSpeed = 50;
    [SerializeField] private float ammo = 50;
    [SerializeField] private float maxAmmo = 50;

    void Update()
    {
        // Allow everyone (host and clients) to shoot
        if (Input.GetMouseButtonDown(0))
        {
            RequestSpawnBulletServerRpc();
        }
    }

    [ServerRpc]
    private void RequestSpawnBulletServerRpc(ServerRpcParams rpcParams = default)
    {
        // Server validates the request and spawns the bullet
        SpawnBulletClientRpc();
    }

    [ClientRpc]
    private void SpawnBulletClientRpc(ClientRpcParams rpcParams = default)
    {
        // Instantiate the bullet on all clients at the correct position and rotation
        GameObject spawnedObj = Instantiate(bullet, firingPoint.position, firingPoint.rotation);
        
        Rigidbody rb = spawnedObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(firingPoint.forward * bulletSpeed, ForceMode.Impulse);
        }
    }
}
