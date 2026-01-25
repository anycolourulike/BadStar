using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableSelectionUI : MonoBehaviour
{
    
    float timer = 3f;
  

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            var selectionUI = this.gameObject;
            selectionUI.SetActive(false);
        }
    }
}