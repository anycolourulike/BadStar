using UnityEngine;
using Rambler.Crew.UI;

namespace Rambler.Crew
{
    //Where the player hires and manages crew: opens the Home Terminal (the CrewScreen).
    //Prefab: Tools > Company > Build Home Terminal.
    public class CrewTerminal : LocationTerminal
    {
        [Tooltip("Open on the Crew tab instead of Recruit.")]
        [SerializeField] bool openOnCrew = false;

        protected override string DefaultLabel => "HOME";
        protected override Color PromptColor => new Color(0.95f, 0.62f, 0.2f);
        protected override bool HasScreen => Home != null;
        protected override bool ScreenOpen => Home != null && Home.IsOpen;
        protected override void OpenScreen() => Home.Open(openOnCrew);

        static CrewScreen Home => CrewScreen.Instance != null ? CrewScreen.Instance : FindObjectOfType<CrewScreen>(true);
    }
}
