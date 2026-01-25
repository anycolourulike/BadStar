using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rambler.Control;

namespace Rambler.UI
{
    public class ShowHideUI : MonoBehaviour
    {        
        [SerializeField] GameObject uiContainer = null;

        void OnEnable()
        {
            uiContainer.SetActive(false);            
        }   
      
        public void ShowInv()
        {                 
            uiContainer.SetActive(true);
        }

        public void HideInv(GameObject newPlayer)
        {                 
            uiContainer.SetActive(false);
        }
    }
}    
