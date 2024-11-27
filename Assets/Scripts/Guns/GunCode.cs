using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunCode : MonoBehaviour
{

    //assigning variables
    [SerializeField] private Transform firingPoint;
    [SerializeField] private GameObject bullet;

    //settings
    [SerializeField] private float bulletSpeed = 50;
    [SerializeField] private float ammo = 50;


    void Start()
    {
        
    }

    void Update()
    {
        if(Input.GetMouseButtonDown(0)){
            SpawnBullet();
        }
    }

    private void SpawnBullet(){
        
    }
}
