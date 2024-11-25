using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BringUpMenu : MonoBehaviour
{
    public GameObject setting;
    public bool issettingactive;
    public Slider slider;

    void Update()
    {

        CamController.rotationSpeed = slider.value*2;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (issettingactive == false)
            {
                Pause();
            }

            else
            {
                Resume();
            }
        }
    }

    public void Pause ()
    {
        setting.SetActive(true);
        issettingactive = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume ()
    {
        setting.SetActive(false);
        issettingactive = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}