using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

public class CrouchTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        {
            var aICon = other.GetComponent<AIController>();

             //aICon.CanCrouch();
             if (aICon != null) aICon.AtCrouchingPoint();
            
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            var aICon = other.GetComponent<AIController>();

            //aICon.CanCrouch();
            //aICon.AtCrouchingPoint();            
        }
    }
}
