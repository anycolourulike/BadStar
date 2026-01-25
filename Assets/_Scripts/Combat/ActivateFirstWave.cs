using Rambler.Control;
using Rambler.Movement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//Diables all mover scripts on 
public class ActivateFirstWave : MonoBehaviour
{
    [SerializeField] GameObject player;
    public TextPopUp textPopUp;
    public string textToPopUp;
    public List<GameObject> enemyObj = new();

    public void OnEnable()
    {
        PlayerSelector.OnPlayerObjectChanged += UpdatePlayer;
    }

    public void OnDisable()
    {
        PlayerSelector.OnPlayerObjectChanged -= UpdatePlayer;
    }   

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == player)
        {           
           foreach (GameObject obj in enemyObj)
           {
              obj.SetActive(true);                    
           }           
        }
        textPopUp = player.GetComponent<TextPopUp>();
        textPopUp.EnablePopUp(textToPopUp);
    }

    public void UpdatePlayer(GameObject newPlayer)
    {
        player = newPlayer;
    }
}
