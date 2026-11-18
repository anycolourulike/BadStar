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
using Rambler.Saving;

namespace Rambler.SceneManagement 
{
    public class Portal : MonoBehaviour
    {
        public enum DestinationIdentifier
        {
            A, B, C, D, E
        }    
        
        [SerializeField] float fadeOutTime = 4f;
        bool transitioning;
        PlayerController playerController;
        LevelManager levelManager;       
        
        public DestinationIdentifier destination;
        public PlayerSelector playerSelector;       
        public Transform companionSpawnPoint;
        public Transform playerSpawnPoint;
        public GameObject activePlayer;
        public GameObject companion;
        public int sceneRef;
        public int introNum;
        public int introSubSceneRef;
        

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
            levelManager = FindObjectOfType<LevelManager>();           
            activePlayer = playerSelector.activePlayer;
            companion = playerSelector.companion;
        }

        void UpdatePlayer(GameObject newPlayer)
        {
            activePlayer = newPlayer;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (transitioning) return; //Re-entering the trigger during the fade must not start a second load
            if (other.gameObject == activePlayer)
            {
                transitioning = true;
                StartCoroutine(Transition());
            }
        }

        private IEnumerator Transition()
        {
            levelManager.destinationID = destination;
            levelManager.sceneRef = sceneRef;
            levelManager.introNum = introNum;

            Fader fader = FindObjectOfType<Fader>();
            SavingWrapper wrapper = FindObjectOfType<SavingWrapper>();
            wrapper.Save();
            //Fade out fully before LoadLoading swaps the scene
            if (fader != null) yield return fader.FadeToBlack(fadeOutTime);

            yield return levelManager.StartCoroutine("LoadLoading");
            Destroy(gameObject);
        }
    }
}
