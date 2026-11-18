using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

//Sits in the Loading scene. Loads the level LevelManager picked in the background while the loading screen shows,
//then, once the level is ready and the screen has been up for at least minDisplayTime, hands it to LevelManager to
//switch in. LevelManager does the switch because activating the level unloads this scene (and this component).
public class Loading : MonoBehaviour
{
   [Tooltip("Shortest time the loading screen stays up (s), so its image can be read")]
   [SerializeField] float minDisplayTime = 4f;

   public AssetReference sceneToLoad;

   void Start()
   {
      sceneToLoad = LevelManager.Instance.sceneToLoad;
      StartCoroutine(nameof(LoadScene));
   }

   public IEnumerator LoadScene()
   {
      float shownUntil = Time.unscaledTime + minDisplayTime;

      //Load without activating, so the loading screen stays up until the switch
      AsyncOperationHandle<SceneInstance> handle =
         Addressables.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single, activateOnLoad: false);
      yield return handle;

      if (handle.Status != AsyncOperationStatus.Succeeded)
      {
         Debug.LogError("Loading: could not load the level " + sceneToLoad);
         yield break;
      }

      while (Time.unscaledTime < shownUntil) yield return null;

      LevelManager.Instance.ActivateLevel(handle);
   }
}
