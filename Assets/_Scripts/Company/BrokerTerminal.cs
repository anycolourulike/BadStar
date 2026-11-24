using UnityEngine;
using Rambler.Crew.UI;

namespace Rambler.Crew
{
    //Where the player buys, upgrades and collects from businesses: opens the Broker screen.
    //Prefab: Tools > Company > Build Shipyard and Broker.
    public class BrokerTerminal : LocationTerminal
    {
        protected override string DefaultLabel => "BROKER";
        protected override Color PromptColor => new Color(0.35f, 0.8f, 0.45f);
        protected override bool HasScreen => BrokerScreen.Find() != null;
        protected override bool ScreenOpen => BrokerScreen.Find() != null && BrokerScreen.Find().IsOpen;
        protected override void OpenScreen() => BrokerScreen.Find().Open();
    }
}
