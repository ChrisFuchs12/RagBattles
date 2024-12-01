using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneThingymajig : MonoBehaviour
{

    public GameObject loadingScrn;
    public GameObject icon;
    public GameObject playButton;

    public void PlayGame(){
        loadingScrn.SetActive(true);
        icon.SetActive(false);
        playButton.SetActive(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
