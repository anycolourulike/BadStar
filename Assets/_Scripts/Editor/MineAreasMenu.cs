using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

//The Mine level is split into an always-loaded core (Mine.unity) and streamed area scenes. In the game the AreaStreamer
//loads the areas around the player; for editing, these menu items open or close all of them alongside the core.
public static class MineAreasMenu
{
    const string Core = "Assets/_Scenes/Mine/Mine.unity";
    static readonly string[] Areas =
    {
        "Assets/_Scenes/Mine/Mine_Surface.unity",
        "Assets/_Scenes/Mine/Mine_Habitat.unity",
        "Assets/_Scenes/Mine/Mine_Mine1.unity",
    };

    [MenuItem("BadStar/Mine/Open Core + All Areas")]
    static void OpenAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (SceneManager.GetActiveScene().path != Core) EditorSceneManager.OpenScene(Core, OpenSceneMode.Single);
        foreach (var a in Areas)
            if (!SceneManager.GetSceneByPath(a).isLoaded) EditorSceneManager.OpenScene(a, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(Core)); //The core holds the sky, fog and lighting
    }

    [MenuItem("BadStar/Mine/Close All Areas")]
    static void CloseAreas()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var a in Areas)
        {
            var s = SceneManager.GetSceneByPath(a);
            if (s.IsValid()) EditorSceneManager.CloseScene(s, true);
        }
    }
}
