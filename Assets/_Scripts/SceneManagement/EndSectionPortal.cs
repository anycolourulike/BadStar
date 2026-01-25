using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AI;
using Rambler.SceneManagement;
using Rambler.Control;
using Rambler.Combat;

namespace Rambler.SceneManagement 
{
    public class EndSectionPortal : MonoBehaviour
    {
        enum DestinationIdentifier
        {
            A, B, C, D, E
        }
        
        [SerializeField] GameObject rambler;
        [SerializeField] float fadeOutTime = 1f;
        PlayerController playerController;
        public AssetReference sceneToLoad;
        Weapon equippedWeapon; 
        GameObject companion;
        GameObject player;
        public int sceneRef;

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject ==  rambler)
            {
                StartCoroutine(Transition());
            }
        }

        private IEnumerator Transition()
        {          

            LevelManager.Instance.sceneRef = sceneRef;
            Fader fader = FindObjectOfType<Fader>();
            SavingWrapper wrapper = FindObjectOfType<SavingWrapper>();
            GameObject HUD = GameObject.FindWithTag("HUD");
            HUD.SetActive(false);
            wrapper.Save(); 

            List<GameObject> playersList = new List<GameObject>();
            playersList.AddRange(collection: GameObject.FindGameObjectsWithTag("Player"));

            foreach(var Item in playersList)
            {
                if(Item.name == "Rambler")
                {
                    player = Item;
                    playerController = player.GetComponent<PlayerController>(); 
                    var fighter = player.GetComponent<Fighter>();
                    equippedWeapon = fighter.weaponConfig;
                }
                else if(Item.name == "Companion")
                {
                    companion = Item;                    
                }
            }         
            
            playerController.enabled = false;
            yield return fader.FadeToClear(fadeOutTime);           
            
            LevelManager.Instance.sceneRef = sceneRef;
            yield return LevelManager.Instance.StartCoroutine("LoadLoading");
            Destroy(gameObject);
        } 
    }
}