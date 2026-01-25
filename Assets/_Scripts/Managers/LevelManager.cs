using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
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
    AsyncOperationHandle<SceneInstance> handle;
    public PlayerSelector playerSelector;
    public CameraManager cameraManager;
    public AssetReference sceneToLoad;
    public GameObject playerCore; 
    
    public GameObject dialogueObj;
    public DialogueUI dialogueUI;
    public Fader fader;

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
    //Menu Scenes
    public AssetReference Loading;
    public AssetReference Menu; 
    public AssetReference Intro;  
    //Freighter
    public AssetReference f3; //Seq
    public AssetReference f2;
    public AssetReference f1;
   
    //Surface
    public AssetReference Crash; //Dial Warbots
    public AssetReference Canyons;
    public AssetReference Cliffs;

    //Weather Station
    public AssetReference outside;
    public AssetReference inside;
    
    //Cave
    public AssetReference Tunnels;
    public AssetReference Cavern; //Dial    
    public AssetReference Rocker; //Boss Rocker...to be continued...
    //Outro
      

    void Awake()
    {        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            GameObject.DontDestroyOnLoad(gameObject);
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
       fader.FadeToClear(3);      
       yield return new WaitForSeconds(0.3f); //Delay to allow loading of next scenes assets
       Addressables.LoadSceneAsync(Loading, LoadSceneMode.Single).Completed += OnSceneLoadComplete; 
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
        System.GC.Collect();
        FindAssetPath();
        AmbientMusic();
        
        if (sceneToLoad == Loading) return;
        if (sceneToLoad == Intro) return;
        if (sceneToLoad == Menu) return;
        FindPlayerSelector();
        StartCoroutine(nameof(LoadSavedFile));
        StartCoroutine(nameof(SetUpLevel));
        if (sceneToLoad == f3)
        {           
            playerSelector.StartingLives();
        }
        if(sceneToLoad == Crash)
        {
            playerSelector.AddOneLife();
        }
        Time.timeScale = 1;
        DestOutput();
    } 

    void OnSceneLoadComplete(AsyncOperationHandle<SceneInstance>previousScene)
    {  
        if(previousScene.Status == AsyncOperationStatus.Succeeded)
        {
          handle = previousScene;
          UnloadScene();
        }  
    }

    void UnloadScene()
    {   
       Addressables.UnloadSceneAsync(handle, true).Completed += op =>
       {
          if(op.Status == AsyncOperationStatus.Succeeded)
          {
               
          }
       };
    } 

    void FindPlayerSelector()
    {
        var playerCore = GameObject.Find("PlayerCore");
        PlayerSelector playerSelector = playerCore.GetComponent<PlayerSelector>();
        activePlayer = playerSelector.activePlayer;
        if (activePlayer != null)
        {
            playerHealth = activePlayer.GetComponent<Health>();
            enemySpawner.FindPopUp();
        }
    }
    
    IEnumerator SetUpLevel()
    {
        GameObject camManObj = GameObject.FindGameObjectWithTag("CamMan");
        cameraManager = camManObj.GetComponent<CameraManager>();
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
            Debug.Log("destCheckFalse");
        }

        fader.canvas.sortingOrder = -1;
        fader.FadeToClear(10f);

        dialogueObj = GameObject.Find(name: "/PlayerCore/HUD/Dialogue");
        dialogueUI = dialogueObj.GetComponent<DialogueUI>();
        if (dialogueUI != null)
        {
            dialogueUI.InitDialogue();
        }
    }

    public void FindAssetPath()
    {
       switch (sceneRef)
       {
         case 0:
             sceneToLoad = Menu;
            break;

         case 1:
            sceneToLoad = Intro;
            break;

         case 2:
            sceneToLoad = f3;
            break;

         case 3:
            sceneToLoad = f2;
            break;

         case 4:
            sceneToLoad = f1;
            break;

         case 5:
            sceneToLoad = Crash;
            break;

         case 6:
            sceneToLoad = Canyons;
            break;

         case 7:
            sceneToLoad = Cliffs;
            break;

         case 8:
            sceneToLoad = outside;
            break;

         case 9:
            sceneToLoad = inside;
            break;

         case 10:
            sceneToLoad = Tunnels;
            break;

         case 11:
            sceneToLoad = Cavern;
            break;

         case 12:
            sceneToLoad = Crash;
            break;       

         case 13:
            sceneToLoad = Rocker;
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
                
                break;

            case 2:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);                           
                break;

            case 3:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);                           
                break;

            case 4:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);
                break;

            case 5:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);
                break;

            case 6:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);                           
                break;

            case 7:
                 AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);                           
                 break;

            case 8:
                 AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);
                 break;

            case 9:
                 AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);
                 break;

            case 10:
                 AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);
                 break;

            case 11:
                 AudioManager.PlayAmbientSound(AudioManager.AmbientSound.RigBackground);
                 break;

            case 12:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 13:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 14:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 15:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 16:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 17:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 18:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 19:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.SurfaceBackground);
                break;

            case 20:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.CaveBackground);
                break;

            case 21:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.CaveBackground);
                break;

            case 22:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.CaveBackground);
                break;

            case 23:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.CaveBackground);
                break;

            case 24:
                AudioManager.PlayAmbientSound(AudioManager.AmbientSound.CaveBackground);
                break;

            case 25:
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

    public void PlusThreeLives()
    {
        playerSelector.AdWatched();
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

