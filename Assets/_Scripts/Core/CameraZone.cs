using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Rambler.Control;

public class CameraZone : MonoBehaviour
{
    [SerializeField] CinemachineVirtualCamera cineCamera;
    public PlayerSelector playerSelector;
    public GameObject player;
    public bool zoomOut;
    
    [SerializeField] float zoomedOutValue = 47f;
    [SerializeField] float zoomedInValue  = 40f;
    float speed = 5f;

    void OnEnable()
    {
        PlayerSelector.OnPlayerObjectChanged += UpdatePlayer;
    }

    void OnDisable()
    {
        PlayerSelector.OnPlayerObjectChanged -= UpdatePlayer;
    }

    void Start()
    {
        player = playerSelector.activePlayer;   
    }

    void Update() 
    {           
        if(zoomOut == true)
        {
            {
              cineCamera.m_Lens.FieldOfView += speed * Time.deltaTime;
            } 
        }
        else if(zoomOut == false)
        {   
            if(cineCamera.m_Lens.FieldOfView > zoomedInValue)
            {
              cineCamera.m_Lens.FieldOfView -= speed * Time.deltaTime;
            } 
        }        
    }

    void UpdatePlayer(GameObject newPlayer)
    {
        player = newPlayer;
    }

    void OnTriggerEnter(Collider other) 
    {
        if(other.gameObject == player)
        {
            zoomOut = true;
        }
    }

    void OnTriggerExit(Collider other)
   {
       if(other.gameObject == player)
        {
            zoomOut = false;
        }
   }
}
