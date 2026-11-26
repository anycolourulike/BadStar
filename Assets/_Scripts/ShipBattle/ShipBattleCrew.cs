using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Movement;
using Rambler.Ship;

namespace Rambler.Crew.Battle
{
    //The crew side of a ship battle:
    //  Station AI    crew at a station the player isn't at run it themselves; skill makes them quicker and surer
    //  Practice      whoever mans a station gets better at its skill (saved on their record)
    //  Takeover      a boarder at an unmanned station turns it on the ship
    //  Medbay        heals the squad inside, powered by Ship Systems, faster with a medic there
    //  Armoury       anyone in the Weapons room restocks their ammo
    //  Looters       some boarders carry the hold's cargo off to their airlock
    public partial class ShipBattle
    {
        [Header("Crew AI")]
        [Tooltip("Seconds between an AI operator's decisions at skill 0 (0.4 s quicker per level, 1.5 s at best).")]
        [SerializeField] float aiThinkSeconds = 6f;
        [Tooltip("Chance an AI operator at skill 0 dithers and does nothing (less per level).")]
        [Range(0f, 1f)] [SerializeField] float aiDitherChance = 0.35f;
        [Tooltip("Practice is this much faster at a station that suits the operator's trait.")]
        [SerializeField] float traitPracticeBonus = 1.5f;

        [Header("Takeover")]
        [SerializeField] float hijackDrainSeconds = 4f;
        [SerializeField] float hijackSystemsSeconds = 5f;

        [Header("Medbay")]
        [SerializeField] string medbayRoom = "Medbay";
        [Tooltip("Share of a patient's full health healed a second at 100% Ship Systems with no medic.")]
        [Range(0f, 0.2f)] [SerializeField] float medbayHealShare = 0.03f;
        [Tooltip("Extra healing per level of the best Medic skill in the medbay.")]
        [SerializeField] float medicPerLevel = 0.15f;

        [Header("Armoury")]
        [SerializeField] string armouryRoom = "Weapons";

        [Header("Looters")]
        [Range(0f, 1f)] [SerializeField] float looterChance = 0.4f;
        [SerializeField] float lootSeconds = 2f;

        readonly Dictionary<Station, float> aiTimers = new Dictionary<Station, float>();
        readonly Dictionary<Station, float> hijackTimers = new Dictionary<Station, float>();
        readonly HashSet<Station> wasHijacked = new HashSet<Station>();
        readonly HashSet<ShipRoom> aiVented = new HashSet<ShipRoom>();
        readonly Dictionary<GameObject, float> lastRestock = new Dictionary<GameObject, float>();
        float hostileCharge, medbayTimer, armouryTimer;

        public int CargoLost { get; private set; }
        public int CratesLost { get; private set; }

        public bool IsHijacked(Station s) { var t = Terminal(s); return t != null && t.Hijacked; }
        public bool IsPlayerControlled(Station s) { var t = Terminal(s); return t != null && t.PlayerControlled; }

        void CrewUpdate(float dt)
        {
            Practice(dt);
            StationAI(dt);
            Takeovers(dt);
            if ((medbayTimer -= dt) <= 0f) { medbayTimer = 0.5f; Medbay(0.5f); }
            if ((armouryTimer -= dt) <= 0f) { armouryTimer = 2f; Armoury(); }
        }

        // ---------- Practice ----------

        void Practice(float dt)
        {
            foreach (var t in terminals.Values)
            {
                if (damaged.Contains(t.Station) || t.Hijacked) continue;
                var record = t.OperatorRecord;
                var skill = t.Skill;
                if (record == null || skill == null) continue;
                if (record.Practise(skill, dt * (t.OperatorHasStationTrait ? traitPracticeBonus : 1f)))
                    Log(record.displayName + "'s " + skill.DisplayName + " is now " + record.GetSkill(skill) + "!");
            }
        }

        // ---------- Station AI ----------

        void StationAI(float dt)
        {
            foreach (var t in terminals.Values)
            {
                var s = t.Station;
                if (t.Operator == null || t.PlayerControlled || damaged.Contains(s)) continue;
                aiTimers.TryGetValue(s, out float timer);
                timer -= dt;
                if (timer > 0f) { aiTimers[s] = timer; continue; }
                int skill = t.OperatorSkill;
                aiTimers[s] = Mathf.Max(1.5f, aiThinkSeconds - 0.4f * skill);
                if (Random.value < Mathf.Max(0f, aiDitherChance - 0.035f * skill)) continue;
                switch (s)
                {
                    case Station.Navigation: ThinkNavigation(); break;
                    case Station.Weapons: ThinkWeapons(); break;
                    case Station.Engines: ThinkEngines(); break;
                    case Station.Systems: ThinkSystems(); break;
                }
            }
        }

        //Run when the hull is failing and the enemy is still strong; stay otherwise
        void ThinkNavigation()
        {
            bool losing = Hull < MaxHull * 0.3f && EnemyHull > enemyHull * 0.4f;
            if (losing && !Escaping) ToggleEscape();
            else if (Escaping && !losing && Hull > MaxHull * 0.5f) ToggleEscape();
        }

        //Stop the boarders, then the guns, then go for the hull
        void ThinkWeapons()
        {
            EnemyTarget want = EnemyTarget.Hull;
            if (!EnemyEnginesDown && (BoardersAlive > 0 || NextBoarding < 15f)) want = EnemyTarget.Engines;
            else if (!EnemyWeaponsDown && Hull < MaxHull * 0.6f) want = EnemyTarget.Weapons;
            if (want != Target) { Target = want; Log("The gunner switches aim to the " + enemyName + "'s " + want.ToString().ToLowerInvariant() + "."); }
        }

        //Fix what's broken, then share power by what the fight needs
        void ThinkEngines()
        {
            if (!Repairing.HasValue)
                foreach (var s in new[] { Station.Engines, Station.Weapons, Station.Navigation, Station.Systems })
                    if (damaged.Contains(s)) { StartRepair(s); break; }

            var want = new Dictionary<Station, int>
            {
                { Station.Weapons, IsManned(Station.Weapons) ? 3 : 1 },
                { Station.Navigation, Escaping ? maxPips : 2 },
                { Station.Systems, breaches.Count > 0 || BoardersAlive > 0 || MedbayPatients() > 0 ? 2 : 1 },
            };
            foreach (var s in Powered) while (pips[s] > want[s]) pips[s]--;
            var order = Escaping ? new[] { Station.Navigation, Station.Weapons, Station.Systems } : new[] { Station.Weapons, Station.Systems, Station.Navigation };
            foreach (var s in order) while (pips[s] < want[s] && pips[s] < maxPips && FreePips > 0) pips[s]++;
            //Anything left over goes where it helps most (not to guns nobody is firing)
            foreach (var s in order) while ((s != Station.Weapons || IsManned(Station.Weapons)) && pips[s] < maxPips && FreePips > 0) pips[s]++;
        }

        //Vent rooms boarders are in when none of the squad is; let the air back in once they're gone
        void ThinkSystems()
        {
            foreach (var room in ShipRoom.All)
            {
                if (room == null || breaches.ContainsKey(room)) continue;
                bool enemies = HasEnemies(room), friends = room.HasPlayerSide();
                if (enemies && !friends && !room.IsVenting)
                {
                    room.VentOxygen();
                    aiVented.Add(room);
                    Log("Ship Systems vents the " + room.roomName + " on the boarders.");
                    return;
                }
                if (aiVented.Contains(room) && (!enemies || friends))
                {
                    room.RestoreAtmosphere();
                    aiVented.Remove(room);
                }
            }
        }

        static bool HasEnemies(ShipRoom room)
        {
            foreach (var o in room.Occupants)
                if (o != null && !ShipCharacters.IsPlayerSide(o) && ShipCharacters.IsAlive(o)) return true;
            return false;
        }

        // ---------- Takeover ----------

        void Takeovers(float dt)
        {
            foreach (var t in terminals.Values)
            {
                var s = t.Station;
                bool hijacked = t.Hijacked;
                if (hijacked && wasHijacked.Add(s)) Log("<color=#E64033>Boarders have taken the " + s + " station!</color>");
                else if (!hijacked && wasHijacked.Remove(s)) Log("The " + s + " station is back in our hands.");
                if (!hijacked) continue;

                hijackTimers.TryGetValue(s, out float timer);
                timer += dt;
                switch (s)
                {
                    case Station.Navigation:
                        if (Escaping) { Escaping = false; Log("The boarders on the bridge cancel the jump!"); }
                        EscapeCharge = Mathf.Max(0f, EscapeCharge - dt / escapeSeconds);
                        break;
                    case Station.Weapons:
                        hostileCharge += dt / reloadSeconds;
                        if (hostileCharge >= 1f) { hostileCharge = 0f; FriendlyFire(); }
                        break;
                    case Station.Engines:
                        if (timer >= hijackDrainSeconds)
                        {
                            timer = 0f;
                            var powered = new List<Station>();
                            foreach (var p in Powered) if (pips[p] > 0) powered.Add(p);
                            if (powered.Count > 0) { var p = powered[Random.Range(0, powered.Count)]; pips[p]--; Log("The boarders in the engine room drain power from " + p + "."); }
                        }
                        break;
                    case Station.Systems:
                        if (timer >= hijackSystemsSeconds) { timer = 0f; HostileSystems(); }
                        break;
                }
                hijackTimers[s] = timer;
            }
        }

        //Our own guns, turned on our own rooms
        void FriendlyFire()
        {
            int dmg = Mathf.Max(1, firepower / 2);
            Hull = Mathf.Max(0, Hull - dmg);
            CameraShake();
            string msg = "<color=#E64033>Our own guns fire into the ship!</color> -" + dmg + " hull.";
            if (ShipRoom.All.Count > 0 && Random.value < 0.3f)
            {
                var room = ShipRoom.All[Random.Range(0, ShipRoom.All.Count)];
                if (room != null && !breaches.ContainsKey(room)) { breaches[room] = breachSeconds; room.VentOxygen(); msg += " Breach in the " + room.roomName + "!"; }
            }
            if (Random.value < 0.25f)
            {
                var candidates = new List<Station>();
                foreach (Station s in System.Enum.GetValues(typeof(Station))) if (!damaged.Contains(s)) candidates.Add(s);
                if (candidates.Count > 0) { var s = candidates[Random.Range(0, candidates.Count)]; damaged.Add(s); ClampPower(); msg += " The " + s + " station is damaged!"; }
            }
            Log(msg);
            if (Hull <= 0) End(BattleState.Lost);
        }

        //Doors thrown open, and the air let out of a room the squad is in
        void HostileSystems()
        {
            foreach (var door in ShipDoor.All)
            {
                if (door == null) continue;
                if (door.IsSealed) door.SetSealed(false);
                door.Open();
            }
            foreach (var room in ShipRoom.All)
            {
                if (room == null || room.IsVenting || !room.HasPlayerSide()) continue;
                room.VentOxygen();
                Log("<color=#E64033>The boarders at Ship Systems vent the " + room.roomName + "!</color>");
                return;
            }
        }

        // ---------- Medbay ----------

        ShipRoom FindRoom(string name)
        {
            foreach (var r in ShipRoom.All) if (r != null && r.roomName == name) return r;
            return null;
        }

        int MedbayPatients()
        {
            var room = FindRoom(medbayRoom);
            if (room == null) return 0;
            int n = 0;
            foreach (var o in room.Occupants)
            {
                if (o == null || !ShipCharacters.IsPlayerSide(o)) continue;
                var h = o.GetComponent<Health>();
                if (h != null && !h.IsDead() && h.GetFraction() < 0.999f) n++;
            }
            return n;
        }

        //Ship Systems powers the medbay: off while it's damaged or taken
        public float MedbayRate
        {
            get
            {
                if (damaged.Contains(Station.Systems) || IsHijacked(Station.Systems)) return 0f;
                return medbayHealShare * Mathf.Max(0.25f, Effect(Station.Systems));
            }
        }

        void Medbay(float seconds)
        {
            var room = FindRoom(medbayRoom);
            if (room == null) return;
            float rate = MedbayRate;
            if (rate <= 0f) return;
            //The best medic in the room speeds everyone up (a Field Medic doubles it)
            float medic = 1f;
            var medicSkill = SkillNamed("Medic");
            foreach (var o in room.Occupants)
            {
                if (o == null || !ShipCharacters.IsPlayerSide(o) || !ShipCharacters.IsAlive(o)) continue;
                var roster = o.GetComponent<RosterMember>();
                var record = roster != null ? roster.Record : null;
                if (record == null) continue;
                float m = 1f + medicPerLevel * (medicSkill != null ? record.GetSkill(medicSkill) : 0);
                if (record.HasTrait(CrewTrait.FieldMedic)) m *= CrewTraits.MedicMultiplier;
                medic = Mathf.Max(medic, m);
            }
            foreach (var o in room.Occupants)
            {
                if (o == null || !ShipCharacters.IsPlayerSide(o)) continue;
                var h = o.GetComponent<Health>();
                if (h != null && !h.IsDead() && h.GetFraction() < 0.999f) h.Heal(h.GetMaxHealth() * rate * medic * seconds);
            }
        }

        static SkillDefinition SkillNamed(string name)
        {
            foreach (var s in Rambler.Ownables.CatalogAsset.All<SkillDefinition>()) if (s.DisplayName == name) return s;
            return null;
        }

        // ---------- Armoury ----------

        void Armoury()
        {
            var room = FindRoom(armouryRoom);
            if (room == null) return;
            foreach (var o in room.Occupants)
            {
                if (o == null || !ShipCharacters.IsPlayerSide(o) || !ShipCharacters.IsAlive(o)) continue;
                var fighter = o.GetComponent<Fighter>();
                var weapon = fighter != null ? fighter.activeWeapon : null;
                if (weapon == null) continue;
                int before = weapon.Reserve;
                weapon.FullAmmo();
                if (weapon.Reserve <= before) continue;
                if (o.CompareTag("Player")) weapon.AmmoUIInit();
                if (!lastRestock.TryGetValue(o, out float last) || Time.time - last > 30f)
                {
                    lastRestock[o] = Time.time;
                    Log(NameOf(o) + " restocks ammo at the armoury.");
                }
            }
        }

        static string NameOf(GameObject o)
        {
            var roster = o.GetComponent<RosterMember>();
            return roster != null && roster.Record != null ? roster.Record.displayName : o.name.Replace("(Clone)", "").Trim();
        }

        // ---------- Boarders ----------

        //Each boarder is a raider (heads for a station: a manned one to fight, an empty one to take) or a looter
        IEnumerator Boarder(GameObject unit, ShipSpawnPoint entry)
        {
            yield return null;
            if (unit == null) yield break;
            var brain = unit.GetComponent<Rambler.Control.EnemyAI>();
            bool looter = Random.value < looterChance && FreeCrate(unit.transform.position) != null;
            if (looter) yield return Loot(unit, brain, entry);
            else yield return Raid(unit, brain);
        }

        IEnumerator Raid(GameObject unit, Rambler.Control.EnemyAI brain)
        {
            var manned = new List<StationTerminal>();
            var empty = new List<StationTerminal>();
            foreach (var t in terminals.Values) (t.Operator != null ? manned : empty).Add(t);
            StationTerminal goal;
            if (empty.Count > 0 && (manned.Count == 0 || Random.value < 0.5f)) goal = empty[Random.Range(0, empty.Count)];
            else if (manned.Count > 0) goal = manned[Random.Range(0, manned.Count)];
            else yield break;
            //It stays at the station once it gets there (its home), unless it sees someone to fight
            if (brain != null && brain.isActiveAndEnabled) brain.SetHome(goal.transform);
            while (State == BattleState.Fighting && ShipCharacters.IsAlive(unit))
            {
                if (Vector3.Distance(unit.transform.position, goal.transform.position) > 1.5f) Send(unit, brain, goal.transform.position);
                yield return new WaitForSeconds(2f);
            }
        }

        IEnumerator Loot(GameObject unit, Rambler.Control.EnemyAI brain, ShipSpawnPoint entry)
        {
            CargoCrate carrying = null;
            float near = 0f;
            while (State == BattleState.Fighting)
            {
                if (!ShipCharacters.IsAlive(unit))
                {
                    if (carrying != null) { carrying.PutBack(); Log("A looter falls and drops the cargo."); }
                    yield break;
                }
                if (carrying == null)
                {
                    var crate = FreeCrate(unit.transform.position);
                    if (crate == null) { yield return Raid(unit, brain); yield break; }
                    crate.ClaimedBy = unit;
                    if (brain != null && brain.isActiveAndEnabled) brain.SetHome(crate.transform);
                    near = 0f;
                    while (State == BattleState.Fighting && ShipCharacters.IsAlive(unit) && !crate.Taken && crate.ClaimedBy == unit)
                    {
                        if (Vector3.Distance(Flat(unit.transform.position), Flat(crate.transform.position)) < 1.6f)
                        {
                            near += 0.5f;
                            if (near >= lootSeconds) { crate.Take(); carrying = crate; Log("A boarder grabs a crate from the hold!"); break; }
                        }
                        else { near = 0f; Send(unit, brain, crate.transform.position); }
                        yield return new WaitForSeconds(0.5f);
                    }
                    continue;
                }
                //Carrying: back to the airlock and away
                if (entry == null) yield break;
                if (brain != null && brain.isActiveAndEnabled) brain.SetHome(entry.transform);
                if (Vector3.Distance(Flat(unit.transform.position), Flat(entry.transform.position)) < 1.6f)
                {
                    CargoLost += carrying.Value;
                    CratesLost++;
                    Log("<color=#E64033>A boarder escapes with a crate of cargo (-" + carrying.Value + " credits).</color>");
                    boarders.Remove(unit);
                    Destroy(unit);
                    yield break;
                }
                Send(unit, brain, entry.transform.position);
                yield return new WaitForSeconds(0.5f);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static void Send(GameObject unit, Rambler.Control.EnemyAI brain, Vector3 goal)
        {
            if (brain != null && brain.isActiveAndEnabled)
            {
                if (!brain.TargetVisible) brain.HearOf(goal);
                return;
            }
            var mover = unit.GetComponent<Mover>();
            if (mover != null) mover.StartMoveAction(goal, 1f);
        }

        static CargoCrate FreeCrate(Vector3 from)
        {
            CargoCrate best = null;
            float bestDist = float.MaxValue;
            foreach (var c in CargoCrate.All)
            {
                if (c == null || !c.Free) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }
    }
}
