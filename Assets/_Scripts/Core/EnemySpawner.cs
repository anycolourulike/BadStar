using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;
using Rambler.Attributes;
using System;
using Rambler.SceneManagement;
using System.Linq;

namespace Rambler.Core
{
    //Reduce amount of loops in SecTeamCount, same action happens twice
    //Reduce DisableTeams invoke time in EnemyCheck
    //Test delay on Disable Team logic
    public class EnemySpawner : MonoBehaviour
    {
        public int deadEnemyCount = 0;       //number of total dead enemies
        public int nextWaveCount;            //count number of enemies when secTeamActivated and assign
        public GameObject activeSecTeam;
        public GameObject endLevelTxt;
        public bool levelLoaded = false;
        public bool nextTeam = false;
      
        public List<GameObject> portals 
            = new List<GameObject>();
        public List<GameObject> secTeam
            = new List<GameObject>();
        public List<GameObject> enemiesList
            = new List<GameObject>();
       
        public void FindPopUp()
        {
            var HUD = GameObject.Find("/PlayerCore/HUD/");
            endLevelTxt = HUD.transform.GetChild(2).gameObject;
        }

        void Update()
        {
            if (levelLoaded == true)
            {
                if (deadEnemyCount == nextWaveCount)
                {
                    if (nextTeam == false)
                    {
                        Debug.Log("5" + " " + "deadEnemy==nextWaveCount");
                        deadEnemyCount = 0;
                        nextWaveCount = 0;
                        secTeam.Remove(activeSecTeam);
                        Destroy(activeSecTeam);
                        activeSecTeam = null;
                        ActivateNextSecTeam();
                        nextTeam = true;
                    }
                }
                if ((secTeam.Count == 0) && (enemiesList.Count == 0))
                {
                    SetPortalsActive();
                    endLevelTxt.SetActive(true);
                }
            }
        }

        public void SetPortalFXInactive()
        {
            portals.Clear();
            GameObject[] taggedPortals = GameObject.FindGameObjectsWithTag("Portal");
            foreach(GameObject obj in taggedPortals)
            {
                portals.Add(obj);
                foreach( GameObject taggedObj in portals)
                {
                    taggedObj.SetActive(false);
                }
            }
        }
        
        public void SetPortalsActive()
        {
            if (endLevelTxt != null)
            {
                endLevelTxt.SetActive(true);
                SetFXActive();
            }
        }

        void SetFXActive()
        {
            foreach (GameObject taggedobj in portals)
            {
                if (taggedobj != null)
                {
                    taggedobj.SetActive(true);
                }
            }
        }
        
        //Runs first called by levelManager.levelload and populates all lists 
        public bool EnemyCheck()
        {
            Debug.Log("1" + " " + "EnemyCheck");
            deadEnemyCount = 0;
            nextWaveCount = 0;
            // Clear the list of secTeam if previously populated
            secTeam.Clear();

            // Find all gameObjects with the tag "SecTeam" and add them to the list secTeam
            GameObject[] secTeamObjects = GameObject.FindGameObjectsWithTag("SecTeam");
            foreach (GameObject secTeamObj in secTeamObjects)
            {
                secTeam.Add(secTeamObj);
                activeSecTeam = secTeam.First();
            }

            Invoke("DisableTeams", 1f);

            SecTeamCount(activeSecTeam, secTeam);
            // Return true if there are any enemies, returns false otherwise
            return (enemiesList.Count > 0);
        }

        // Finds all child Objects of activeSecTeam with tag "Enemy".
        // If none it disables game object assigned to activeSecTeam & sets it to null
        // Finally it assigns a value to nextWaveCount
        void SecTeamCount(GameObject activeSecTeam, List<GameObject> secTeam)
        {
            Debug.Log("3" + " " + "CountEnemies");
            enemiesList.Clear();
            if (CountEnemiesInChildren(activeSecTeam) == 0)
            {
                deadEnemyCount = 0;
                nextWaveCount = 0;
                return;
            }

            // Find all children of activeSecTeam with tag "Enemy"
            Transform[] enemies = activeSecTeam.GetComponentsInChildren<Transform>()
                .Where(child => child.CompareTag("Enemy"))
                .ToArray();

            //If there are no enemies found
            if (enemies.Length == 0)
            {
                // Disable activeSecTeam
                activeSecTeam.SetActive(false);
                secTeam.Remove(activeSecTeam);
                Destroy(activeSecTeam);
                activeSecTeam = null;
            }
            else
            {
                foreach(Transform transform in enemies)
                {
                    enemiesList.Add(transform.gameObject);
                }
                nextWaveCount += enemies.Length;
            }
           
            levelLoaded = true;
            nextTeam = false;
        }

        //disables all but 1 SecTeam
        void ActivateNextSecTeam()
        {
            Debug.Log("4" + " " + "ActivateNextSecTeam");
            if (secTeam.Count > 0)
            {
                GameObject subsequentTeam = secTeam.First();
                subsequentTeam.SetActive(true);
                activeSecTeam = subsequentTeam;
                if (subsequentTeam != null)
                {
                    SecTeamCount(subsequentTeam, secTeam);
                }
            }

            Invoke("DisableTeams", .3f);
            
        }

        //Removes Enemy and checks parent object if empty
        public void RemoveEnemy(GameObject enemyToRemove)
        {
            Debug.Log("2" + " " + "RemoveEnemy");
            for (int i = enemiesList.Count - 1; i >= 0; i--)
            {
                if (enemiesList[i] == enemyToRemove)
                { 
                    enemiesList.RemoveAt(i);
                    enemyToRemove.transform.SetParent(null);
                    deadEnemyCount++;
                    
                    if (deadEnemyCount == nextWaveCount)
                    {
                        nextTeam = false;                        
                    }
                    break;
                }
            }
        }

        void DisableTeams()
        {
            for (int i = 1; i < secTeam.Count; i++)
            {
                secTeam[i].SetActive(false);
            }
        }

        int CountEnemiesInChildren(GameObject activeSecTeam)
        {
            int enemyCount = 0;
            foreach (Transform child in activeSecTeam.transform)
            {
                if (child.CompareTag("Enemy"))
                {
                    enemyCount++;
                }
            }
            Debug.Log("EnemyCount" + " " + enemyCount);
            return enemyCount;
        }
    }
}
