using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Core;
using Rambler.Attributes;
using Rambler.Movement;
using Rambler.Combat;

public class HealthRegenScript : MonoBehaviour
{
    [SerializeField] GameObject healthRegenFX;
    PlayerVitals playerVitals;
    Health playerHealth;    
    Mover mover;    
    
   void OnTriggerEnter(Collider other) 
   {
       if(other.gameObject.tag == "Player")
       {
           playerHealth = other.GetComponent<Health>();
           //var curHP = playerHealth.HealthPoints;
           //if (curHP == 100f) return;

           healthRegenFX.SetActive(true);
           playerVitals = other.GetComponent<PlayerVitals>();                           
           mover = other.GetComponent<Mover>();         
           mover.CancelNav();
           playerVitals.Restore();                      
       }     
   }

   void OnTriggerExit(Collider other) 
   {
       if(other.gameObject.tag == "Player")
       {
           healthRegenFX.SetActive(false);
       }
   }
}
