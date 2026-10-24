using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Saving;
using Rambler.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Rambler.Core;

public class MenuScene : MonoBehaviour
{    
    [SerializeField] AssetReference sceneToLoad;
    [SerializeField] GameObject mainCam;
    LevelManager levelManager;
    SavingWrapper wrapper;
    Fader fader;    
    int sceneRef = 2; //New game starts at the Crash Site

    void Start()
    {
        wrapper = FindObjectOfType<SavingWrapper>();
        fader = FindObjectOfType<Fader>();
        fader.FadeToBlackImmediate();
        levelManager = FindObjectOfType<LevelManager>(); 
        levelManager.OnLevelFinishedLoading();
        fader.FadeToClear(3);
    }  

    public void LoadNextLevel()
    {
        wrapper.Delete();
        FindMusic();
        fader.FadeToClear(3);
        levelManager.sceneRef = sceneRef;
        levelManager.StartCoroutine("LoadLoading");  
    }

    public void LoadSavedGame() 
    {  
       //wrapper.Load();
        fader.FadeToClear(3);
        FindMusic();
        LevelManager.Instance.lifeWasLost = true;
        wrapper.ContinueGame(); 
    }

    public void QuitApp()
    {
        FindMusic();
        fader.FadeToClear(3);
        levelManager.StartCoroutine("QuitApp");
    }

    void FindMusic()
    {
        var music = GameObject.Find("AmbientSFX");
        music.SetActive(false); 
        AudioManager.PlayHumanSound(AudioManager.HumanSound.Death4, mainCam.transform.position);
    }           
}
