using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneThingymajig : MonoBehaviour
{

    public GameObject loadingScrn;

    public void PlayGame(){
        loadingScrn.SetActive(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
