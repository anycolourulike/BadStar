using UnityEngine;
using Rambler.Ownables;
using Rambler.UI;

namespace Rambler.Crew.Battle
{
    public enum Station { Navigation, Weapons, Engines, Systems }

    //One of the four stations aboard the player's ship. Whoever stands at it mans it (the active player first,
    //otherwise any of the squad standing there) and their skill sets how well it works: Piloting for Navigation,
    //Firearms for Weapons, Engineering for Engines and Ship Systems; a matching trait adds to it.
    //  The active player at a station sets it by hand (tap the prompt for its screen; Ship Systems opens the ship map).
    //  Anyone else there runs it on their own (ShipBattle's station AI), and gets better with practice.
    //  An enemy standing at an unmanned station takes it over and uses it against the ship.
    //Prefab: Tools > Company > Build Ship Battle.
    public class StationTerminal : LocationTerminal
    {
        [SerializeField] Station station = Station.Navigation;
        [Tooltip("Skill level used for characters who aren't Company crew (e.g. Rambler).")]
        [SerializeField] int defaultSkill = 3;

        public Station Station => station;

        //An enemy standing at the station (checked a few times a second)
        public GameObject Intruder { get; private set; }
        //Taken over: an enemy is at it and none of the squad is
        public bool Hijacked => Intruder != null && Operator == null;
        //Fought over: an enemy and one of the squad are both at it
        public bool Contested => Intruder != null && Operator != null;
        //Set by hand: the active player is the one at it
        public bool PlayerControlled { get { var op = Operator; return op != null && op == ActivePlayer; } }

        Collider area;
        float scanTimer;
        static readonly Collider[] hits = new Collider[24];

        protected override void Start()
        {
            base.Start();
            area = GetComponent<Collider>();
        }

        protected override void Update()
        {
            base.Update();
            if ((scanTimer -= Time.deltaTime) > 0f) return;
            scanTimer = 0.25f;
            Intruder = null;
            if (area == null) return;
            var b = area.bounds;
            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Rambler.Ship.ShipCharacters.Root(hits[i]);
                if (c == null || Rambler.Ship.ShipCharacters.IsPlayerSide(c) || !Rambler.Ship.ShipCharacters.IsAlive(c)) continue;
                Intruder = c;
                break;
            }
        }

        public CharacterRecord OperatorRecord
        {
            get
            {
                var op = Operator;
                var roster = op != null ? op.GetComponent<RosterMember>() : null;
                return roster != null ? roster.Record : null;
            }
        }

        //The trait that suits this station, if the operator has it
        public bool OperatorHasStationTrait => HasStationTrait(OperatorRecord);

        public bool HasStationTrait(CharacterRecord r)
        {
            if (r == null) return false;
            foreach (var t in r.Traits) if (CrewTraits.StationIndex(t) == (int)station) return true;
            return false;
        }

        public bool HasStationTrait(GameObject c) => HasStationTrait(RecordOf(c));

        static CharacterRecord RecordOf(GameObject c)
        {
            var roster = c != null ? c.GetComponent<RosterMember>() : null;
            return roster != null ? roster.Record : null;
        }

        //How well this character would run the station: their level in its skill plus the trait bonus
        public int SkillOf(GameObject c)
        {
            if (c == null) return 0;
            var record = RecordOf(c);
            if (record == null) return defaultSkill;
            var skill = Skill;
            int level = skill != null ? record.GetSkill(skill) : defaultSkill;
            return level + (HasStationTrait(record) ? CrewTraits.StationBonus : 0);
        }

        //Where whoever works it stands: the middle of its area, on the floor
        public Vector3 StandPoint
        {
            get
            {
                var c = area != null ? area : GetComponent<Collider>();
                if (c == null) return transform.position;
                var p = c.bounds.center;
                p.y = transform.position.y;
                return p;
            }
        }

        public string DisplayName => station == Station.Systems ? "Systems" : station.ToString();

        public SkillDefinition Skill
        {
            get
            {
                foreach (var skill in CatalogAsset.All<SkillDefinition>())
                    if (skill.DisplayName == SkillName) return skill;
                return null;
            }
        }

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

        //The operator's level in this station's skill, plus their trait's bonus (0 when unmanned)
        public int OperatorSkill => SkillOf(Operator);

        protected override string DefaultLabel => station == Station.Systems ? "SYSTEMS" : station.ToString().ToUpperInvariant();
        protected override Color PromptColor => ColorFor(station);
        //Ship Systems opens the live top-down view of the ship (the older room map if that screen isn't built)
        protected override bool HasScreen => station == Station.Systems ? SystemsScreen.Find() != null || Map != null : StationScreen.Find() != null;
        protected override bool ScreenOpen => station == Station.Systems
            ? (SystemsScreen.Find() != null ? SystemsScreen.Find().IsOpen : Map != null && Map.IsOpen)
            : StationScreen.Find() != null && StationScreen.Find().IsOpenFor(this);

        protected override void OpenScreen()
        {
            if (station != Station.Systems) StationScreen.Find().Open(this);
            else if (SystemsScreen.Find() != null) SystemsScreen.Find().Open();
            else Map.Open();
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
