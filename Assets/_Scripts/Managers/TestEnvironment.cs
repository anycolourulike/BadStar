#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

//Editor-only test environment. Pressing Play on a level scene (Mine, CrashSite) normally skips the
//Preloader > Menu > Loading chain, so the managers that live in the Menu scene (LevelManager,
//SavingSystem, EnemySpawner...) are missing. This brings in the same Managers prefab and sets the
//level up the way Loading does, so a level can be tested on its own. Toggle it under
//Tools ▸ Test Environment. Not compiled into builds.
public class TestEnvironment : MonoBehaviour
{
    public const string EnabledKey = "Rambler.TestEnvironment.Enabled";
    public const string LoadSaveKey = "Rambler.TestEnvironment.LoadSave";
    const string ManagersPrefabPath = "Assets/_Prefabs/Managers/Managers.prefab";

    //Level scenes by name, and the LevelManager.sceneRef the game uses for each
    static readonly (string scene, int sceneRef)[] Levels =
    {
        ("CrashSite", 2),
        ("Mine", 3),
    };

    public static bool Enabled => EditorPrefs.GetBool(EnabledKey, true);
    public static bool LoadSave => EditorPrefs.GetBool(LoadSaveKey, false);

    static int pendingSceneRef = -1;

    //Before the level's own objects wake, so they find the LevelManager just as in the real game
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadManagers()
    {
        pendingSceneRef = -1;
        if (!Enabled || LevelManager.Instance != null) return; //Turned off

        int sceneRef = SceneRefFor(SceneManager.GetActiveScene().name);
        if (sceneRef < 0) return; //Preloader, Menu, Loading look after themselves

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagersPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[TestEnvironment] " + ManagersPrefabPath + " is missing; playing without managers.");
            return;
        }

        var managers = Instantiate(prefab);
        managers.name = prefab.name; //LevelManager makes it persist, as in the real game
        managers.GetComponent<LevelManager>().sceneRef = sceneRef;
        pendingSceneRef = sceneRef;
    }

    //Once the level is loaded, finish setting it up the way the Loading scene does
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SetUpLoadedLevel()
    {
        if (pendingSceneRef < 0) return;
        var runner = new GameObject("[TestEnvironment]").AddComponent<TestEnvironment>();
        runner.StartCoroutine(runner.SetUpLevel(pendingSceneRef));
        pendingSceneRef = -1;
    }

    static int SceneRefFor(string sceneName)
    {
        foreach (var level in Levels)
        {
            if (level.scene == sceneName) return level.sceneRef;
        }
        return -1;
    }

    IEnumerator SetUpLevel(int sceneRef)
    {
        yield return null; //Let the managers' Start find the scene's objects first
        var levelManager = LevelManager.Instance;
        levelManager.sceneRef = sceneRef;
        levelManager.OnLevelFinishedLoading(LoadSave);
        Debug.Log("[TestEnvironment] Managers loaded for " + SceneManager.GetActiveScene().name +
                  " (sceneRef " + sceneRef + (LoadSave ? ", save file loaded" : ", no save file") + ")");
        Destroy(gameObject);
    }
}
#endif
