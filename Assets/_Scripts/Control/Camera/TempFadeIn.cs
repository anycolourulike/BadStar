using Rambler.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TempFadeIn : MonoBehaviour
{
    public float fade;
    public Fader fader;
    public GameObject interact;   


    void Start()
    {
        if(fader != null)
        {
            fader.FadeToClear(fade);
            Invoke("DisableInteract", 2f);
        }
    }  

    void DisableInteract()
    {
        interact.SetActive(false);
    }    
}
