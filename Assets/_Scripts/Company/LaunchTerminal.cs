using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rambler.Crew
{
    //Takes the squad up in the active ship: tapping the prompt loads a scene (the ship battle for now).
    //The scene must be in Build Settings. Prefab: Tools > Company > Build Ship Battle.
    public class LaunchTerminal : LocationTerminal
    {
        [SerializeField] string sceneName = "ShipBattle";

        protected override string DefaultLabel => "LAUNCH";
        protected override Color PromptColor => new Color(0.9f, 0.3f, 0.25f);
        protected override bool HasScreen => true;
        protected override bool ScreenOpen => false;

        protected override void OpenScreen()
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName)) SceneManager.LoadScene(sceneName);
            else Debug.LogWarning("[LaunchTerminal] " + sceneName + " isn't in Build Settings.");
        }
    }
}
