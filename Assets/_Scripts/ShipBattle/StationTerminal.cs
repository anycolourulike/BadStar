using UnityEngine;
using Rambler.Ownables;
using Rambler.UI;

namespace Rambler.Crew.Battle
{
    public enum Station { Navigation, Weapons, Engines, Systems }

    //One of the four stations aboard the player's ship. Whoever stands at it mans it (the active player first,
    //otherwise any of the squad standing there) and their skill sets how well it works: Piloting for Navigation,
    //Firearms for Weapons, Engineering for Engines and Ship Systems. Tap the prompt to open the station's screen;
    //Ship Systems opens the ship map. Prefab: Tools > Company > Build Ship Battle.
    public class StationTerminal : LocationTerminal
    {
        [SerializeField] Station station = Station.Navigation;
        [Tooltip("Skill level used for characters who aren't Company crew (e.g. Rambler).")]
        [SerializeField] int defaultSkill = 3;

        public Station Station => station;

        //Who is working the station right now (null = unmanned)
        public GameObject Operator
        {
            get
            {
                var active = ActivePlayer;
                GameObject first = null;
                foreach (var c in Inside)
                {
                    if (c == active) return c;
                    if (first == null) first = c;
                }
                return first;
            }
        }

        public string OperatorName
        {
            get
            {
                var op = Operator;
                if (op == null) return null;
                var roster = op.GetComponent<RosterMember>();
                var record = roster != null ? roster.Record : null;
                return record != null ? record.displayName : op.name.Replace("(Clone)", "").Trim();
            }
        }

        public string SkillName => SkillFor(station);

        public static string SkillFor(Station s)
        {
            switch (s)
            {
                case Station.Navigation: return "Piloting";
                case Station.Weapons: return "Firearms";
                default: return "Engineering";
            }
        }

        //The operator's level in this station's skill (0 when unmanned)
        public int OperatorSkill
        {
            get
            {
                var op = Operator;
                if (op == null) return 0;
                var roster = op.GetComponent<RosterMember>();
                var record = roster != null ? roster.Record : null;
                if (record == null) return defaultSkill;
                foreach (var skill in CatalogAsset.All<SkillDefinition>())
                    if (skill.DisplayName == SkillName) return record.GetSkill(skill);
                return defaultSkill;
            }
        }

        protected override string DefaultLabel => station == Station.Systems ? "SYSTEMS" : station.ToString().ToUpperInvariant();
        protected override Color PromptColor => ColorFor(station);
        protected override bool HasScreen => station == Station.Systems ? Map != null : StationScreen.Find() != null;
        protected override bool ScreenOpen => station == Station.Systems ? Map != null && Map.IsOpen : StationScreen.Find() != null && StationScreen.Find().IsOpenFor(this);

        protected override void OpenScreen()
        {
            if (station == Station.Systems) Map.Open();
            else StationScreen.Find().Open(this);
        }

        static ShipMapPanel Map => FindObjectOfType<ShipMapPanel>(true);

        public static Color ColorFor(Station s)
        {
            switch (s)
            {
                case Station.Navigation: return new Color(0.3f, 0.65f, 0.95f);
                case Station.Weapons: return new Color(0.9f, 0.3f, 0.25f);
                case Station.Engines: return new Color(0.95f, 0.62f, 0.2f);
                default: return new Color(0.35f, 0.8f, 0.45f);
            }
        }
    }
}
