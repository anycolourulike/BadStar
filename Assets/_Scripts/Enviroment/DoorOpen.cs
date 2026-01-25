using System;
using System.Collections;
using System.Collections.Generic;
using Rambler.Control;
using UnityEngine;

public class DoorOpen : MonoBehaviour
{
    public Transform targetWaypoint;
    public delegate void DoorUnlocked();
    public static DoorUnlocked doorUnlocked;    
    public bool doorIsUnlocked;
    public AIController aIController;

    public float doorTimer = 7f;
    public bool doorIsOpen;
    
    bool doorOpened;
    Animator anim;

    void OnEnable()
    {
        doorUnlocked += DoorLock;
    }   

    void OnDisable()
    {
        doorUnlocked -= DoorLock;
    }

    void Start() 
    {
        anim = GetComponent<Animator>();      
    } 

    void Update()
    {
        DoorTimer();
        if(doorIsUnlocked == true && doorOpened == false)
        {         
            OpenDoor();
            doorOpened = true;
        }         

        if(doorTimer <= 0f)
        {
            doorTimer = 7f;
            doorIsUnlocked = false;  
            doorIsOpen = false; 
            doorOpened = false;         
            anim.SetTrigger("Close");
        } 
    } 

    void DoorLock()
    {
       doorIsUnlocked = true;
    }  

    void DoorTimer()
    {
        if(doorIsOpen == true)
        {
          doorTimer -= Time.deltaTime;
        }  
    }

    void OnTriggerEnter(Collider other)
    {
        if((other.gameObject.CompareTag("Enemy")) || (other.gameObject.CompareTag("Player")) || (other.gameObject.CompareTag("Companion")))
        {
            anim.SetTrigger("Open");
            doorIsOpen = true;
            if (other.gameObject.CompareTag("Player"))
            {
                UpdateTargetVector(targetWaypoint);
            }    
        }
    }

    private void UpdateTargetVector(Transform targetWaypoint)
    {
        var playerCore = GameObject.Find("PlayerCore");
        var playerSelector = playerCore.GetComponent<PlayerSelector>();
        aIController = playerSelector.companion.GetComponent<AIController>();
        aIController.UpdatePatrolToTarget(targetWaypoint);
    }

    void OpenDoor()
    {
        anim.SetTrigger("Open");
        doorIsOpen = true;
    }
}
