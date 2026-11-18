using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Rambler.Core;
using Rambler.SceneManagement;
using Rambler.Saving;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Attributes;
using Rambler.UI;


public class LevelManager : MonoBehaviour, ISaveable
{
    public static LevelManager Instance { set; get; }
    // --- SCENE TRACKING ---
    // This stores the handle for the scene currently active in the game.
    // We use this to properly release memory when moving to a new area.
    AsyncOperationHandle<SceneInstance> activeSceneHandle;
    
    [Header("Rambler System References")]
    public PlayerSelector playerSelector;
    public AssetReference sceneToLoad;
    public SceneRefs sceneRefs;
    
    public GameObject dialogueObj;
    public DialogueUI dialogueUI;
    
    [Header("State Tracking")]
    public GameObject activePlayer;
    public Health playerHealth;
    public bool lifeWasLost;
    public EnemySpawner enemySpawner;
    SavingWrapper wrapper;

    public Portal.DestinationIdentifier destinationID;
    public int introNum;
    public int sceneRef;
    public int introSubSceneRef = 2; //loads robot lab after first seq
    public Transform spawnPoint;
    
    [Header("Addressable Scene Assets")]
    //Menu Scenes
    public AssetReference Loading;
    public AssetReference Menu;
    //Levels
    [FormerlySerializedAs("f3")] public AssetReference crashSite;
    public AssetReference mine;


    void Awake()
    {        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }            
    }  

    void Start()
    {
        enemySpawner = FindObjectOfType<EnemySpawner>();
        wrapper = FindObjectOfType<SavingWrapper>();
    }

    void DestOutput()
    {
        Debug.Log(destinationID.ToString());
    }

    public IEnumerator LoadLoading()
    {
       FindAssetPath();
       yield return new WaitForSecondsRealtime(0.3f); //Delay to allow loading of next scenes assets

       //Let a fade-out that is already running (portal, menu, continue) reach black before the scene swaps
       Fader fader = FindObjectOfType<Fader>();
       float waited = 0f;
       while (fader != null && fader.IsFading && waited < 5f)
       {
           waited += Time.unscaledDeltaTime;
           yield return null;
       }

       //Use loadedSceneHandle to match your convention and yield it to ensure completion
       AsyncOperationHandle<SceneInstance> loadedSceneHandle = 
           Addressables.LoadSceneAsync(Loading, LoadSceneMode.Single);
       loadedSceneHandle.Completed += OnSceneLoadComplete; 
       yield return loadedSceneHandle;
    } 
    
    public IEnumerator LoadMenu() 
    { 
       yield return new WaitForSeconds(0.3f);
       wrapper.LoadMenu();       
    }

    public IEnumerator LoadSavedGame() 
    {          
        yield return StartCoroutine(nameof(LoadLoading));
    } 

    //Called by Loading once the next level has loaded in the background. Switches it in from here because
    //LevelManager survives the switch, while the Loading scene (and its component) is unloaded by it.
    public void ActivateLevel(AsyncOperationHandle<SceneInstance> levelHandle)
    {
        StartCoroutine(ActivateLevelRoutine(levelHandle));
    }

    IEnumerator ActivateLevelRoutine(AsyncOperationHandle<SceneInstance> levelHandle)
    {
        yield return levelHandle.Result.ActivateAsync();
        //Track the level's handle like the Loading scene's, so the next change releases it
        OnSceneLoadComplete(levelHandle);
        OnLevelFinishedLoading();
    }

    void LoadSavedFile()
    {
        wrapper.Load();
        if (lifeWasLost == true)
        {
            lifeWasLost = false;
            if (playerHealth != null) playerHealth.RestoreHealth();
        }
    }

    public IEnumerator QuitApp()
    {   
        yield return new WaitForSecondsRealtime(2);     
        Application.Quit();
    }    

    public void OnLevelFinishedLoading()
    {
        OnLevelFinishedLoading(loadSave: true);
    }

    //The test environment sets a level up without loading the save file, so it starts as placed
    public void OnLevelFinishedLoading(bool loadSave)
    {
        System.GC.Collect();
        FindAssetPath();
        AmbientMusic();
        FindSceneRefs();
        if (sceneToLoad == Menu) return;
        //LevelManager outlives every scene, so find the new level's spawner each time (null in levels without one)
        enemySpawner = FindObjectOfType<EnemySpawner>();
        if (wrapper == null) wrapper = FindObjectOfType<SavingWrapper>();
        FindPlayerSelector();

        StartCoroutine(SetUpLevel(loadSave));
        Time.timeScale = 1;
        DestOutput();
    } 

    void OnSceneLoadComplete(AsyncOperationHandle<SceneInstance>loadedSceneHandle)
    {  
        //Check if loading of new scene was successful
        if(loadedSceneHandle.Status == AsyncOperationStatus.Succeeded)
        {
            //Check if we are currently tracking an older scene
            if (activeSceneHandle.IsValid())
            {
                //Remove the old scene from memory before moving to the new one
                Addressables.UnloadSceneAsync(activeSceneHandle);
            }
          activeSceneHandle = loadedSceneHandle;
        }  
    }

    void FindPlayerSelector()
    {
        var fader = sceneRefs.fader;
        playerSelector = sceneRefs.playerSelector;
        activePlayer = playerSelector.activePlayer;
        if (activePlayer != null)
        {
            playerHealth = activePlayer.GetComponent<Health>();
            if (enemySpawner != null) enemySpawner.FindPopUp();
            //The level's fader is saved inactive; switch it on so the level stays black until SetUpLevel fades in
            fader.gameObject.SetActive(true);
        }
    }

    void FindSceneRefs()
    {
        sceneRefs = FindObjectOfType<SceneRefs>();
    }

    //Runs in order once a level is in: wait for streamed areas, restore the save, put the player at the portal
    //they came through, fade in, then (after the dead-AI settle time) check enemies and portals and hook up dialogue
    IEnumerator SetUpLevel(bool loadSave)
    {
        float settleUntil = Time.time + 2.8f; //Time for restored dead enemies to HealthCheck and remove themselves

        //Streamed levels: wait for the areas around the player, so saveables inside them exist (gives up after 15 s)
        float waited = 0f;
        while (!AreaStreamer.InitialAreasReady && waited < 15f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        //Two frames so every new object has run Awake and Start (Portal.Start picks up the active player)
        yield return null;
        yield return null;

        if (loadSave) LoadSavedFile();
        bool arrivedByPortal = PlaceAtDestination();

        if (sceneRefs != null && sceneRefs.fader != null) sceneRefs.fader.FadeToClear(2f);

        while (Time.time < settleUntil) yield return null;

        if (arrivedByPortal && enemySpawner != null)
        {
            Debug.Log("DestinationCheckTrue");
            if (enemySpawner.EnemyCheck() == true)
            {
                enemySpawner.SetPortalFXInactive();
                if (enemySpawner.endLevelTxt != null)
                {
                    enemySpawner.endLevelTxt.SetActive(false);
                }
            }
            else
            {
                enemySpawner.SetPortalsActive();
            }
        }
        else
        {
            Debug.Log("destinationCheckFalse");
        }

        dialogueObj = GameObject.Find(name: "/PlayerCore/HUD/Dialogue");
        if (dialogueObj == null) yield break; //Level was left before setup finished
        dialogueUI = dialogueObj.GetComponent<DialogueUI>();
        if (dialogueUI != null)
        {
            dialogueUI.InitDialogue();
        }
    }

    //Scene numbers: 0 Menu, 2 Crash Site, 3 Mine.
    //1 was the Intro; old saves that point at it go to the Crash Site.
    public void FindAssetPath()
    {
       switch (sceneRef)
       {
         case 1:
         case 2:
            sceneToLoad = crashSite;
            break;

         case 3:
            sceneToLoad = mine;
            break;

         default:
            sceneToLoad = Menu;
            break;
       }       
    }       

    void AmbientMusic()
    {
        switch(sceneRef)
        {
            case 0:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.ambientMusic);                           
                break;

            case 1:
            case 2:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);                           
                break;

            case 3:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.CaveBackground);
                break;
        }        
    }

    public List<GameObject> FindPortals()
    {
        List<GameObject> foundPortals = new List<GameObject>();
        Portal[] portals = FindObjectsOfType<Portal>();

        foreach (Portal portal in portals)
        {
            foundPortals.Add(portal.gameObject);
        }
        return foundPortals;
    }

    //Puts the active player at the spawn point of the portal matching destinationID. False if the level has none.
    bool PlaceAtDestination()
    {
        foreach (Portal portalScript in FindObjectsOfType<Portal>())
        {
            if (portalScript.destination != destinationID) continue;

            spawnPoint = portalScript.playerSpawnPoint;
            var player = activePlayer != null ? activePlayer : portalScript.activePlayer;
            if (player == null || spawnPoint == null) return false;

            var agent = player.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(spawnPoint.position);
            else player.transform.position = spawnPoint.position;
            return true;
        }
        return false;
    }

    public object CaptureState()
    {
        Dictionary<string, object> data = new Dictionary<string, object>();
        data["destination"] = destinationID;
        data["introNum"] = introNum;
        data["sceneRef"] = sceneRef;
        data["lifeWasLost"] = lifeWasLost;
        return data;
    }

    public void RestoreState(object state)
    {
        Debug.Log("RestoreStateCalled");
        Dictionary<string, object> data = (Dictionary<string, object>)state;
        destinationID = (Portal.DestinationIdentifier)data["destination"];
        introNum = (int)data["introNum"];
        sceneRef = (int)data["sceneRef"];
        lifeWasLost = (bool)data["lifeWasLost"];
    }
}

