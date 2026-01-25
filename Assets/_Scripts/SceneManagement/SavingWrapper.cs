using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rambler.Saving;
using Rambler.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Rambler.SceneManagement
{  
    public class SavingWrapper : MonoBehaviour
    {
        const string currentSaveKey = "currentSaveName";
        float fadeOutTime = 4f;

        public void ContinueGame() 
        {
            StartCoroutine(nameof(LoadLastScene));
        }

        public void LoadMenu()
        {
            StartCoroutine(nameof(LoadMenuScene));
        }

        private IEnumerator LoadLastScene()
        {
            Fader fader = FindObjectOfType<Fader>();
            yield return fader.FadeToClear(fadeOutTime);
            yield return GetComponent<SavingSystem>().LoadLastScene(currentSaveKey);
            //assign sceneRef
            //intro Ref
        }

        private IEnumerator LoadFirstScene()
        {
            Fader fader = FindObjectOfType<Fader>();
            yield return fader.FadeToClear(fadeOutTime);            
            LevelManager.Instance.sceneRef = 2;
            LevelManager.Instance.StartCoroutine("LoadLoading");
        }

        private IEnumerator LoadMenuScene()
        {
            Fader fader = FindObjectOfType<Fader>();
            yield return fader.FadeToClear(fadeOutTime);
            LevelManager.Instance.sceneRef = 0;
            LevelManager.Instance.StartCoroutine("LoadLoading");
        }
        
        public void Save()
        {
            Debug.Log("Game Saved");
            GetComponent<SavingSystem>().Save(currentSaveKey);
        }

        public void Load()
        {
            Debug.Log("Game Loaded");
            GetComponent<SavingSystem>().Load(currentSaveKey);
        }

        public void Delete()
        {
            GetComponent<SavingSystem>().Delete(currentSaveKey);
        }

        public IEnumerable<string> ListSaves()
        {
            return GetComponent<SavingSystem>().ListSaves();
        }
    }
}