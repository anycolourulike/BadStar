using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Core 
{  
  
  public class EnemyDeathCounter : MonoBehaviour
  {
    public Collider col;
    public GameObject fx;
    public int enemyWaveCount;    
    public int numberToTriggerDialogue;

    void Start()
    {
      col.enabled = false;  
      fx.SetActive(false);
    }

    void Update()
    {
      //var enemiesKilled = EnemySpawner.count;
      //if(enemiesKilled == 3)
      //{
      //  enemyWaveCount ++;
      //}   
      if(enemyWaveCount == numberToTriggerDialogue)
      {
        col.enabled = true;
        fx.SetActive(true);
      } 
    }
  } 
}
