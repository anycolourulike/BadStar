using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Characters : MonoBehaviour
{
    public List<GameObject> enemiesList = new();
    public List<GameObject> playersList = new();
    //Series of Methods that provide Info about the teams
    //Saving
    //Characters activated by the group but removed one at a time


    public void RemoveEnemy(GameObject enemyToRemove)
    {
        if(enemiesList.Contains(enemyToRemove))
        {
            enemiesList.Contains(enemyToRemove);
        }
        else
        {
            Debug.Log("Enemy not in List");
        }
    }

    public void RemovePlayer(GameObject playerToRemove)
    {
        if (playersList.Contains(playerToRemove))
        {
            playersList.Contains(playerToRemove);
        }
        else
        {
            Debug.Log("Player not in List");
        }
    }
}
