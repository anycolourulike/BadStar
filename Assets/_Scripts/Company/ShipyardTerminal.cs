using UnityEngine;
using Rambler.Crew.UI;

namespace Rambler.Crew
{
    //Where the player buys, fits, repairs and sends out ships: opens the Shipyard screen.
    //Prefab: Tools > Company > Build Shipyard and Broker.
    public class ShipyardTerminal : LocationTerminal
    {
        protected override string DefaultLabel => "SHIPYARD";
        protected override Color PromptColor => new Color(0.3f, 0.65f, 0.95f);
        protected override bool HasScreen => ShipyardScreen.Find() != null;
        protected override bool ScreenOpen => ShipyardScreen.Find() != null && ShipyardScreen.Find().IsOpen;
        protected override void OpenScreen() => ShipyardScreen.Find().Open();
    }
}
