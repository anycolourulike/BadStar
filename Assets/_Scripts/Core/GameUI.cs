using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Saving;
using Rambler.SceneManagement;
using UnityEngine.EventSystems;


public class GameUI : MonoBehaviour
{
    Interstitial interstitial;
    LevelManager levelManager;
    RewardedAds rewardedAds;
    SavingWrapper wrapper;

    void Start()
    {
        wrapper = FindObjectOfType<SavingWrapper>();
        levelManager = FindObjectOfType<LevelManager>();
        interstitial = FindObjectOfType<Interstitial>();
        rewardedAds = FindObjectOfType<RewardedAds>();
    }

    public void PauseGame()
    {
        Time.timeScale = 0;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1;
    }

    public void DefeatedByTimbertoes()
    {
        ResumeGame();
        wrapper.Save();
        levelManager.sceneRef = 2; //Back to the Crash Site
        levelManager.StartCoroutine(nameof(LoadSavedGame));
    }

    //public void WatchAD()
    //{
    //    Debug.Log("WatchAd Called");
    //    rewardedAds.showRewarded();
    //}

    public void NoADS()
    {
        //InfiniteLives
        //Noads
        Debug.Log("noadscaled");
    }
    public void LoadSavedGame() 
    {
        ResumeGame();
        levelManager.StartCoroutine(nameof(LoadSavedGame));   
    }

    public void LoadMenu() 
    {  
        ResumeGame();      
        levelManager.StartCoroutine(nameof(LoadMenu));
    }

    public void GameOver()
    {
        wrapper.Delete();
        ResumeGame();
        levelManager.StartCoroutine(nameof(LoadMenu));
    }

    public void GameOverQuitApp()
    {
        SavingWrapper wrapper = FindObjectOfType<SavingWrapper>();
        wrapper.Delete();
        ResumeGame();
        levelManager.StartCoroutine(nameof(QuitApp));
    }

    public void QuitApp()
    {
        ResumeGame();
        levelManager.StartCoroutine(nameof(QuitApp));
    }

}
