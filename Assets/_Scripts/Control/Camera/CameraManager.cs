using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Rambler.Control;

public class CameraManager : MonoBehaviour 
{ 

    public List<CinemachineVirtualCamera> vCams = new();
    public List<GameObject> players = new();
    public CinemachineVirtualCamera activeCam; //assigned onlevelLoad and when player enters a trigger
    public GameObject activePlayer; //set from levelManager on load
    public GameObject companion;

    public void OnEnable()
    {
        PlayerSelector.OnPlayerObjectChanged += UpdatePlayer;
    }

    public void OnDisable()
    {
        PlayerSelector.OnPlayerObjectChanged -= UpdatePlayer;
    }

    public void UpdatePlayer(GameObject newPlayer)
    {
        activePlayer = newPlayer;
    }

    public void UpdateFollow(Transform newFollow)
    {
        activeCam.Follow = newFollow;
        activeCam.LookAt = newFollow;
    }

    public void SwitchPriority(CinemachineVirtualCamera newVC)
    {
        if (activeCam == newVC) return;
        activeCam = newVC;
        var pc = activePlayer.GetComponent<PlayerController>();
        pc.cineMachine = activeCam;
        if(activeCam != null)
        {
            foreach(CinemachineVirtualCamera VC in vCams)
            {
                if(VC != activeCam)
                {
                    VC.Priority = 0;
                }
            }
            activeCam.Priority = 2;
        }
    }
}
