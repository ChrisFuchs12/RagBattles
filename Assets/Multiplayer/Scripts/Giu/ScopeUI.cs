using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScopeUI : MonoBehaviour
{
    public GameObject scopeImage;


    
    void Update()
    {
        if(Input.GetMouseButton(1)){
            scopeImage.SetActive(true);
        }
        else{
            scopeImage.SetActive(false);
        }
    }
}
