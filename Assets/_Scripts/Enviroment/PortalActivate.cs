using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalActivate : MonoBehaviour
{
   public Collider col;

   void OnEnable()
    {
        DoorOpen.doorUnlocked += activatePortal;
    }
    
    void OnDisable()
    {
        DoorOpen.doorUnlocked -= activatePortal;
    }

    void activatePortal()
    {
        col.enabled = true;
    }
}
