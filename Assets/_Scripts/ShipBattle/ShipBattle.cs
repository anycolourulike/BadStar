using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Ship;
using Rambler.UI;

namespace Rambler.Crew.Battle
{
    public enum BattleState { Fighting, Won, Escaped, Lost }
    public enum EnemyTarget { Hull, Weapons, Engines, Bridge, Shields }

    //A fight aboard the player's ship against an enemy ship outside. One per battle scene, next to the ShipAI
    //(which is handed to the player so the ship map works on your own ship).
    //  The enemy fires a volley every few seconds: hits cost hull and can damage a station or breach a room.
    //  It sends boarding parties in through the spawn points.
    //  Each station works only as well as whoever mans it (their skill) and the power Engines gives it:
    //    Navigation  dodges a share of each volley; can charge an escape jump
    //    Weapons     charges and fires at the enemy's hull, weapons or engines (needs someone on it)
    //    Engines     shares the ship's power pips out, and repairs damaged stations
    //    Systems     the ship map (doors, air) and how fast breaches are patched
    //  The active player at a station sets it by hand; crew at a station run it themselves (ShipBattleCrew.cs).
    //  Boarders raid stations (an empty one gets taken over and turned on the ship) or loot the hold.
    //  Win: the enemy hull reaches 0. Escape: the jump charges. Lose: your hull reaches 0 or the squad falls.
    public partial class ShipBattle : MonoBehaviour
    {
        public static ShipBattle Instance { get; private set; }
        public event Action<string> Logged;
        public event Action Changed;

        [Header("Your ship (when the Company has no active ship)")]
        [SerializeField] string fallbackName = "Shearwater";
        [SerializeField] int fallbackHull = 400;
        [SerializeField] int fallbackPower = 5;
        [SerializeField] int fallbackFirepower = 40;

        [Header("Stations")]
        [SerializeField] int maxPips = 4;
        [Tooltip("How well an unmanned station works (Weapons never fires unmanned).")]
        [SerializeField] float unmannedFactor = 0.3f;
        [SerializeField] float reloadSeconds = 8f;
        [Tooltip("Seconds for the escape jump to charge at 100% Navigation.")]
        [SerializeField] float escapeSeconds = 60f;
        [Tooltip("Seconds to repair a station at 100% Engines.")]
        [SerializeField] float repairSeconds = 10f;
        [SerializeField] float evadePerEffect = 0.15f;
        [SerializeField] float maxEvade = 0.5f;
        [Tooltip("Seconds a breach vents its room at 100% Systems.")]
        [SerializeField] float breachSeconds = 20f;

        [Header("Enemy ship")]
        [SerializeField] string enemyName = "Raider";
        [SerializeField] int enemyHull = 400;
        [Tooltip("Health of its weapons and of its engines; at 0 they're down for a while.")]
        [SerializeField] int enemySystemHealth = 100;
        [SerializeField] float systemDownSeconds = 20f;
        [SerializeField] float enemyEvade = 0.15f;
        [SerializeField] float volleySeconds = 12f;
        [SerializeField] int shotsPerVolley = 3;
        [SerializeField] Vector2Int shotDamage = new Vector2Int(8, 14);
        [Range(0f, 1f)] [SerializeField] float stationHitChance = 0.1f;
        [Range(0f, 1f)] [SerializeField] float breachChance = 0.08f;

        [Header("Boarders")]
        [SerializeField] GameObject[] boarderPrefabs = new GameObject[0];
        [SerializeField] float firstBoarding = 30f;
        [SerializeField] float boardingSeconds = 45f;
        [SerializeField] int boardersPerWave = 1;
        [SerializeField] int maxBoarders = 6;

        [Header("Outcome")]
        [SerializeField] int victoryReward = 1500;
        [SerializeField] string returnScene = "Shipyard";

        static readonly Station[] Powered = { Station.Weapons, Station.Navigation, Station.Systems };

        readonly Dictionary<Station, int> pips = new Dictionary<Station, int>();
        readonly HashSet<Station> damaged = new HashSet<Station>();
        readonly Dictionary<Station, StationTerminal> terminals = new Dictionary<Station, StationTerminal>();
        readonly Dictionary<ShipRoom, float> breaches = new Dictionary<ShipRoom, float>();
        readonly List<GameObject> boarders = new List<GameObject>();
        ShipRecord record;
        float volleyTimer, boardingTimer, weaponsDown, enginesDown, repairProgress;
        int firepower, waves, boarderIndex;

        public BattleState State { get; private set; } = BattleState.Fighting;
        public string ShipName { get; private set; }
        public int MaxHull { get; private set; }
        public int Hull { get; private set; }
        public int Power { get; private set; }
        public int Firepower => firepower;
        //The most loaded launcher (the old station screen and HUD use it)
        public float WeaponCharge { get { float c = 0f; foreach (var l in launchers) if (l.left > 0) c = Mathf.Max(c, l.charge); return c; } }
        public float EscapeCharge { get; private set; }
        public bool Escaping { get; private set; }
        public EnemyTarget Target { get; private set; } = EnemyTarget.Hull;
        public Station? Repairing { get; private set; }
        public float RepairProgress => repairProgress;
        public string EnemyName => enemyName;
        public int EnemyMaxHull => enemyHull;
        public int EnemyHull { get; private set; }
        public int EnemySystemMax => enemySystemHealth;
        public int EnemyWeapons { get; private set; }
        public int EnemyEngines { get; private set; }
        public bool EnemyWeaponsDown => weaponsDown > 0f;
        public bool EnemyEnginesDown => enginesDown > 0f;
        public float NextVolley => volleyTimer;
        public float NextBoarding => boardingTimer;
        public int BoardersAlive { get { boarders.RemoveAll(b => !ShipCharacters.IsAlive(b)); return boarders.Count; } }
        public int Reward => victoryReward;
        public int MaxPips => maxPips;
        public string LastLog { get; private set; } = "";

        void Awake()
        {
            Instance = this;
            //The ship map is a station: it only works while the player is at Ship Systems (crew there run it themselves)
            ShipMapPanel.Access = () => State == BattleState.Fighting && IsPlayerControlled(Station.Systems) && Effect(Station.Systems) > 0f;
            ShipMapPanel.AccessNote = "";
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ShipMapPanel.Access = null;
            ShipMapPanel.AccessNote = null;
            if (State != BattleState.Fighting) Time.timeScale = 1f;
        }

        void Start()
        {
            foreach (var t in FindObjectsOfType<StationTerminal>()) terminals[t.Station] = t;
            MapStationRooms();
            if (ShipAI.Instance != null) ShipAI.Instance.SetController(ShipFaction.Player);

            var company = Company.Instance;
            record = company != null ? company.ActiveShip : null;
            if (record != null && record.Definition != null)
            {
                var s = record.Stats;
                ShipName = record.DisplayName;
                MaxHull = record.MaxHull;
                Hull = record.Hull;
                Power = Mathf.Max(1, s.power);
                firepower = Mathf.Max(1, s.firepower);
            }
            else
            {
                ShipName = fallbackName;
                MaxHull = Hull = fallbackHull;
                Power = fallbackPower;
                firepower = fallbackFirepower;
            }

            //Power starts spread evenly, weapons first, after a shield layer if there's enough (ShipBattleShields)
            foreach (var s in Powered) pips[s] = 0;
            shieldPips = Power >= 5 ? pipsPerLayer : 0;
            for (int i = 0; i < Power - shieldPips; i++)
            {
                var s = Powered[i % Powered.Length];
                if (pips[s] < maxPips) pips[s]++;
            }

            EnemyHull = enemyHull;
            EnemyWeapons = EnemyEngines = enemySystemHealth;
            volleyTimer = volleySeconds;
            boardingTimer = firstBoarding;
            WeaponsStart();
            ShieldsStart();
            Log("The " + enemyName + " is closing in. Man the stations!");
        }

        void Update()
        {
            if (State != BattleState.Fighting) return;
            float dt = Time.deltaTime;
            ShipMapPanel.AccessNote = damaged.Contains(Station.Systems) ? "(damaged)" : "";

            //Navigation: the escape jump
            if (Escaping)
            {
                EscapeCharge = Mathf.Min(1f, EscapeCharge + Effect(Station.Navigation) * dt / escapeSeconds);
                if (EscapeCharge >= 1f) { End(BattleState.Escaped); return; }
            }

            //Weapons, scanning and the tactical map (ShipBattleWeapons)
            WeaponsUpdate(dt);
            ShieldsUpdate(dt);
            if (State != BattleState.Fighting) return;

            //Engines: repairs
            if (Repairing.HasValue)
            {
                if (!damaged.Contains(Repairing.Value)) Repairing = null;
                else if (IsManned(Station.Engines))
                {
                    repairProgress += CrewFactor(Station.Engines) * dt / repairSeconds;
                    if (repairProgress >= 1f)
                    {
                        damaged.Remove(Repairing.Value);
                        Log(Repairing.Value + " station repaired.");
                        Repairing = null;
                        repairProgress = 0f;
                        ClampPower();
                    }
                }
            }

            //Systems: breaches get patched
            if (breaches.Count > 0)
            {
                float rate = BreachPatchRate * dt;
                foreach (var room in new List<ShipRoom>(breaches.Keys))
                {
                    breaches[room] -= rate;
                    if (breaches[room] > 0f) continue;
                    breaches.Remove(room);
                    if (room != null) { room.RestoreAtmosphere(); Log("Breach in the " + room.roomName + " patched."); }
                }
            }

            //The enemy
            if (weaponsDown > 0f && (weaponsDown -= dt) <= 0f) { EnemyWeapons = enemySystemHealth / 2; Log("The " + enemyName + "'s weapons are back online."); }
            if (enginesDown > 0f && (enginesDown -= dt) <= 0f) { EnemyEngines = enemySystemHealth / 2; Log("The " + enemyName + "'s engines are back online."); }
            if (!EnemyWeaponsDown && (volleyTimer -= dt) <= 0f) { volleyTimer = volleySeconds; StartCoroutine(Volley()); }
            if (!EnemyEnginesDown && (boardingTimer -= dt) <= 0f) { boardingTimer = boardingSeconds; Board(); }

            CrewUpdate(dt);
            RoomsUpdate(dt);
            if (State != BattleState.Fighting) return;
            if (!SquadAlive()) End(BattleState.Lost);
            Changed?.Invoke();
        }

        // ---------- Stations ----------

        public StationTerminal Terminal(Station s) => terminals.TryGetValue(s, out var t) ? t : null;
        public bool IsManned(Station s) { var t = Terminal(s); return t != null && t.Operator != null; }
        public bool IsDamaged(Station s) => damaged.Contains(s);
        public IEnumerable<Station> DamagedStations => damaged;

        //What the crew bring: 60% at skill 0, 100% at 5, 140% at 10; unmanned stations limp along (Weapons not at all).
        //A station boarders have taken does nothing for us; one being fought over works at half.
        public float CrewFactor(Station s)
        {
            if (damaged.Contains(s) || IsEmped(s)) return 0f;
            var t = Terminal(s);
            if (t != null && t.Hijacked) return 0f;
            if (t == null || t.Operator == null) return s == Station.Weapons ? 0f : unmannedFactor;
            return (0.6f + 0.08f * t.OperatorSkill) * (t.Contested ? 0.5f : 1f);
        }

        //Crew times power: two pips is normal, four is double. Engines has no pips.
        public float Effect(Station s)
        {
            float crew = CrewFactor(s);
            if (s == Station.Engines) return crew;
            return crew * Pips(s) / 2f;
        }

        //On the tactical map, dodging comes from how fast the ship is actually going
        public float Evade => (HasTacticalMap ? maxEvade * Mathf.Clamp01(space.playerShip.SpeedShare * 1.1f)
                                              : Mathf.Min(maxEvade, evadePerEffect * Effect(Station.Navigation))) * (Escaping ? 0.5f : 1f);
        public float ReloadTime => Effect(Station.Weapons) > 0f ? reloadSeconds / Effect(Station.Weapons) : Mathf.Infinity;
        //Breaches patch themselves at half speed even with nobody on Systems
        float BreachPatchRate => Mathf.Max(0.5f, Effect(Station.Systems));
        public float BreachRepairTime => breachSeconds / BreachPatchRate;

        //"Cockpit 12s, Hold 30s" for the battle bar
        public string BreachSummary
        {
            get
            {
                if (breaches.Count == 0) return null;
                var parts = new List<string>();
                foreach (var pair in breaches)
                    if (pair.Key != null) parts.Add(pair.Key.roomName + " " + Mathf.CeilToInt(pair.Value / BreachPatchRate) + "s");
                return string.Join(", ", parts);
            }
        }

        // ---------- Power ----------

        //A damaged engine room gives half the power
        public int AvailablePower => damaged.Contains(Station.Engines) ? Power / 2 : Power;
        public int Pips(Station s) => pips.TryGetValue(s, out int p) ? p : 0;
        public int FreePips { get { int used = shieldPips; foreach (var p in pips.Values) used += p; return AvailablePower - used; } }

        //Power can only be moved by whoever is at the Engines station
        public bool CanChangePower => IsManned(Station.Engines) && State == BattleState.Fighting;

        public bool AddPip(Station s)
        {
            if (!CanChangePower || !pips.ContainsKey(s) || FreePips <= 0 || pips[s] >= maxPips) return false;
            pips[s]++;
            Changed?.Invoke();
            return true;
        }

        public bool RemovePip(Station s)
        {
            if (!CanChangePower || !pips.ContainsKey(s) || pips[s] <= 0) return false;
            pips[s]--;
            Changed?.Invoke();
            return true;
        }

        //When power drops (engines hit), pips come out of Shields, Navigation, Weapons and Systems in turn, but Systems keeps
        //its last pip (life support) until there's nothing else to take. When power comes back and Systems has none,
        //it gets one back on its own, so the air doesn't wait for someone to man Engines.
        void ClampPower()
        {
            var order = new[] { Station.Navigation, Station.Weapons, Station.Systems };
            int i = 0;
            while (FreePips < 0 && i < 20)
            {
                if (shieldPips > 0) shieldPips--;
                foreach (var s in order) if (FreePips < 0 && pips[s] > (s == Station.Systems ? 1 : 0)) pips[s]--;
                i++;
            }
            if (FreePips < 0 && pips[Station.Systems] > 0) pips[Station.Systems]--;
            if (pips[Station.Systems] == 0 && FreePips > 0) pips[Station.Systems] = 1;
        }

        // ---------- Orders from the station screens ----------

        public void SetTarget(EnemyTarget t) { Target = t; Changed?.Invoke(); }

        public void ToggleEscape()
        {
            if (State != BattleState.Fighting) return;
            Escaping = !Escaping;
            Log(Escaping ? "Plotting an escape jump. Evasion is halved while it charges." : "Escape cancelled.");
        }

        public bool StartRepair(Station s)
        {
            if (!damaged.Contains(s)) return false;
            if (Repairing != s) repairProgress = 0f;
            Repairing = s;
            Log("Repairing the " + s + " station" + (IsManned(Station.Engines) ? "." : ". Someone has to man Engines to do it."));
            return true;
        }

        // ---------- Combat ----------

        IEnumerator Volley()
        {
            for (int i = 0; i < shotsPerVolley && State == BattleState.Fighting; i++)
            {
                int dmg = UnityEngine.Random.Range(shotDamage.x, shotDamage.y + 1);
                if (SpawnIncoming(dmg)) { } //Flies across the map and lands later
                else if (UnityEngine.Random.value < Evade) Log("Evaded a shot!");
                else if (!OurShieldsStop()) TakeHit(dmg);
                yield return new WaitForSeconds(0.4f);
            }
        }

        void TakeHit(int dmg)
        {
            Hull = Mathf.Max(0, Hull - dmg);
            CameraShake();
            //The shot lands in a room: what it does there depends on the room (RoomHit in ShipBattleRooms)
            var hitRoom = RandomRoom();
            string msg = "Hit" + (hitRoom != null ? " in the " + hitRoom.roomName : "") + "! -" + dmg + " hull.";
            if (hitRoom != null) { msg += RoomHitEffects(hitRoom); Log(msg); if (Hull <= 0) End(BattleState.Lost); return; }
            if (UnityEngine.Random.value < stationHitChance)
            {
                var candidates = new List<Station>();
                foreach (Station s in Enum.GetValues(typeof(Station))) if (!damaged.Contains(s)) candidates.Add(s);
                if (candidates.Count > 0)
                {
                    var s = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                    damaged.Add(s);
                    ClampPower();
                    msg += " The " + s + " station is damaged!";
                }
            }
            if (UnityEngine.Random.value < breachChance && ShipRoom.All.Count > 0)
            {
                var room = ShipRoom.All[UnityEngine.Random.Range(0, ShipRoom.All.Count)];
                if (!breaches.ContainsKey(room))
                {
                    breaches[room] = breachSeconds;
                    room.VentOxygen();
                    msg += " Hull breach in the " + room.roomName + "!";
                }
            }
            Log(msg);
            if (Hull <= 0) End(BattleState.Lost);
        }

        public bool IsBreached(ShipRoom room) => breaches.ContainsKey(room);

        void Board()
        {
            if (boarderPrefabs == null || boarderPrefabs.Length == 0) return;
            var points = new List<ShipSpawnPoint>();
            foreach (var p in ShipSpawnPoint.All) if (p.Serves(ShipUnit.Crew)) points.Add(p);
            if (points.Count == 0) return;
            int count = Mathf.Min(boardersPerWave + waves / 2, maxBoarders - BoardersAlive);
            if (count <= 0) return;
            waves++;
            var spawner = FindObjectOfType<EnemySpawner>();
            for (int i = 0; i < count; i++)
            {
                var prefab = boarderPrefabs[boarderIndex++ % boarderPrefabs.Length];
                if (prefab == null) continue;
                var entry = points[UnityEngine.Random.Range(0, points.Count)];
                var unit = entry.Spawn(prefab, transform);
                unit.name = prefab.name + " (Boarder)";
                unit.tag = "Enemy";
                boarders.Add(unit);
                if (spawner != null) spawner.RegisterEnemy(unit);
                StartCoroutine(Boarder(unit, entry));
            }
            Log(count + " boarder" + (count == 1 ? "" : "s") + " cutting in through the hull!");
        }

        bool SquadAlive()
        {
            foreach (var tag in new[] { "Player", "Companion" })
                foreach (var c in GameObject.FindGameObjectsWithTag(tag))
                    if (ShipCharacters.IsAlive(c)) return true;
            return false;
        }

        void CameraShake()
        {
            var cam = Camera.main;
            if (cam != null) StartCoroutine(Shake(cam.transform));
        }

        IEnumerator Shake(Transform cam)
        {
            Vector3 offset = Vector3.zero;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                cam.position -= offset;
                offset = UnityEngine.Random.insideUnitSphere * 0.15f;
                cam.position += offset;
                yield return null;
            }
            cam.position -= offset;
        }

        // ---------- Ending ----------

        void End(BattleState result)
        {
            if (State != BattleState.Fighting) return;
            State = result;
            StopAllCoroutines();
            if (record != null)
            {
                var company = Company.Instance;
                record.damage = Mathf.Clamp(record.MaxHull - Mathf.Max(1, Hull), 0, record.MaxHull - 1);
                if (result == BattleState.Won && company != null) company.AddCredits(victoryReward);
                else company?.AddCredits(0); //Tells the screens something changed
            }
            else if (result == BattleState.Won && Company.Instance != null) Company.Instance.AddCredits(victoryReward);
            //Cargo the boarders got away with comes off the Company's credits
            if (CargoLost > 0 && Company.Instance != null) Company.Instance.Spend(Mathf.Min(CargoLost, Company.Instance.Credits));

            switch (result)
            {
                case BattleState.Won: Log("The " + enemyName + " breaks apart. Victory!"); break;
                case BattleState.Escaped: Log("Jump! We got away."); break;
                default: Log(Hull <= 0 ? "The ship is crippled." : "The squad is down."); break;
            }
            Changed?.Invoke();
            Time.timeScale = 0f;
        }

        //Back to the Shipyard (or wherever returnScene points)
        public void Leave()
        {
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(returnScene)) SceneManager.LoadScene(returnScene);
        }

        void Log(string message)
        {
            LastLog = message;
            Debug.Log("[ShipBattle] " + message);
            Logged?.Invoke(message);
        }
    }
}
