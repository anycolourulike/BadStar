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
       yield return new WaitForSeconds(0.3f); //Delay to allow loading of next scenes assets
       
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

    IEnumerator LoadSavedFile() 
    {
        //Wait for scene to load and for lists to populate
        yield return new WaitForSeconds(.2f);
        wrapper.Load();
        if (lifeWasLost == true)
        {
            lifeWasLost = false;
            playerHealth.RestoreHealth();
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
        if (sceneToLoad == Loading) return;
        if (sceneToLoad == Menu) return;
        if (sceneRef != null)
        {
            FindPlayerSelector();
        }
        
        if (loadSave) StartCoroutine(nameof(LoadSavedFile));
        StartCoroutine(nameof(SetUpLevel));
        if (sceneToLoad == crashSite)
        {
            playerSelector.StartingLives();
        }
        if (sceneToLoad == mine)
        {
            playerSelector.AddOneLife();
        }
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
        //Store on the field; StartingLives/AddOneLife use it after this returns
        playerSelector = sceneRefs.playerSelector;
        activePlayer = playerSelector.activePlayer;
        if (activePlayer != null)
        {
            playerHealth = activePlayer.GetComponent<Health>();
            enemySpawner.FindPopUp();
            //The level's fader is saved inactive; switch it on so it can fade in from black
            fader.gameObject.SetActive(true);
            fader.FadeToClear(2f);
        }
    }

    void FindSceneRefs()
    {
        sceneRefs = FindObjectOfType<SceneRefs>();
    }
    
    IEnumerator SetUpLevel()
    {
        //Waiting for level enemies to HealthCheck and remove dead AI
        yield return new WaitForSeconds(2.8f);
        if (DestinationCheck() == true)
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

    bool DestinationCheck()
    {
        List<GameObject> portals = FindPortals();

        foreach (GameObject portal in portals)
        {
            Portal portalScript = portal.GetComponent<Portal>();
            if (portalScript.destination == destinationID)
            {
                spawnPoint = portalScript.playerSpawnPoint;
                var player = portalScript.activePlayer;
                player.GetComponent<NavMeshAgent>().enabled = false;
                player.transform.position = spawnPoint.transform.position;
                player.GetComponent<NavMeshAgent>().enabled = true;
                return true;
            }
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

