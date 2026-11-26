using System.Collections.Generic;
using UnityEngine;
using Rambler.Attributes;
using Rambler.Control;
using Rambler.Ship;

namespace Rambler.Crew.Battle
{
    //What can be wrong in a room, as the Ship Systems screen shows it
    public enum RoomAlert { Fire, Breach, Intruders, Emp, Damaged, NoPower, LowOxygen }

    //Hits land in rooms (the ShipBattle partial). Each enemy shot that gets through picks a room of the ship:
    //  the station in that room can be damaged, a fire can start, the hull can breach (the room vents), or an EMP
    //  knocks the room's system out for a while.
    //Fires grow, burn the people inside, wreck the room's station if left, spread through open doors, and die when
    //  the room runs out of air (vent it) or the crew in the room put them out.
    //Crew in a room patch its breach faster and repair its damaged station by hand (as well as Engines' repairs).
    //Oxygen is part of Ship Systems: while that station is damaged, EMP'd, taken or unpowered, the air in every room
    //  slowly runs down.
    public partial class ShipBattle
    {
        [Header("Room hits")]
        [Tooltip("Chance a hit in a station's room damages that station.")]
        [Range(0f, 1f)] [SerializeField] float roomStationHitChance = 0.3f;
        [Range(0f, 1f)] [SerializeField] float fireChance = 0.15f;
        [Range(0f, 1f)] [SerializeField] float empChance = 0.08f;
        [SerializeField] float empSeconds = 12f;

        [Header("Fire")]
        [Tooltip("How fast a fire grows (0 to 1) each second.")]
        [SerializeField] float fireGrowth = 0.04f;
        [Tooltip("How much each crew member in the room puts out each second.")]
        [SerializeField] float firefightRate = 0.1f;
        [Tooltip("A fire this big can spread through open doors.")]
        [SerializeField] float fireSpreadAt = 0.7f;
        [Tooltip("Chance a second of a big fire spreads to each room next door.")]
        [SerializeField] float fireSpreadChance = 0.05f;
        [Tooltip("Damage a second to everyone in a room at full fire.")]
        [SerializeField] float fireDamage = 4f;
        [Tooltip("Seconds of a big fire that wreck the room's station.")]
        [SerializeField] float fireWrecksAfter = 12f;
        [Tooltip("Fire needs air: it dies when the room's oxygen falls below this.")]
        [SerializeField] float fireNeedsOxygen = 25f;

        [Header("Hands-on repairs")]
        [Tooltip("Seconds for one crew member in the room to repair its station.")]
        [SerializeField] float handRepairSeconds = 20f;
        [Tooltip("Each crew member in a breached room patches it this much faster.")]
        [SerializeField] float crewPatchBonus = 1f;
        [Tooltip("Oxygen % below which a room shows LOW O2.")]
        [SerializeField] float lowOxygenAt = 60f;

        readonly Dictionary<ShipRoom, float> fires = new Dictionary<ShipRoom, float>();
        readonly Dictionary<ShipRoom, float> fireBurn = new Dictionary<ShipRoom, float>();
        readonly Dictionary<ShipRoom, float> emps = new Dictionary<ShipRoom, float>();
        readonly Dictionary<Station, ShipRoom> stationRooms = new Dictionary<Station, ShipRoom>();
        readonly Dictionary<Station, float> handRepair = new Dictionary<Station, float>();
        bool lifeSupportWas = true;

        //A shot landed in this room (the Systems screen flashes it)
        public event System.Action<ShipRoom> RoomHit;

        void MapStationRooms()
        {
            stationRooms.Clear();
            foreach (var pair in terminals)
            {
                var room = ShipRoom.At(pair.Value.StandPoint + Vector3.up * 0.5f);
                if (room == null) room = ShipRoom.At(pair.Value.transform.position + Vector3.up * 0.5f);
                if (room != null) stationRooms[pair.Key] = room;
            }
        }

        public ShipRoom RoomOf(Station s) => stationRooms.TryGetValue(s, out var r) ? r : null;

        public Station? StationIn(ShipRoom room)
        {
            foreach (var pair in stationRooms) if (pair.Value == room) return pair.Key;
            return null;
        }

        public float FireLevel(ShipRoom room) => room != null && fires.TryGetValue(room, out float f) ? f : 0f;
        public bool OnFire(ShipRoom room) => room != null && fires.ContainsKey(room);
        public float EmpLeft(ShipRoom room) => room != null && emps.TryGetValue(room, out float t) ? t : 0f;
        public bool IsEmped(Station s) { var r = RoomOf(s); return r != null && emps.ContainsKey(r); }
        public int FireCount => fires.Count;

        //The ship's oxygen plant is part of Ship Systems
        public bool LifeSupportOnline
        {
            get
            {
                if (damaged.Contains(Station.Systems) || IsEmped(Station.Systems) || Pips(Station.Systems) <= 0) return false;
                var t = Terminal(Station.Systems);
                return t == null || !t.Hijacked;
            }
        }

        //Everything wrong in a room right now
        public void Alerts(ShipRoom room, List<RoomAlert> into)
        {
            into.Clear();
            if (room == null) return;
            if (OnFire(room)) into.Add(RoomAlert.Fire);
            if (breaches.ContainsKey(room)) into.Add(RoomAlert.Breach);
            if (HasEnemies(room)) into.Add(RoomAlert.Intruders);
            if (emps.ContainsKey(room)) into.Add(RoomAlert.Emp);
            var s = StationIn(room);
            if (s.HasValue && damaged.Contains(s.Value)) into.Add(RoomAlert.Damaged);
            if (s.HasValue && s.Value != Station.Engines && Pips(s.Value) <= 0) into.Add(RoomAlert.NoPower);
            if (room.Oxygen < lowOxygenAt) into.Add(RoomAlert.LowOxygen);
        }

        public static string AlertLabel(RoomAlert a)
        {
            switch (a)
            {
                case RoomAlert.Fire: return "FIRE";
                case RoomAlert.Breach: return "BREACH";
                case RoomAlert.Intruders: return "INTRUDERS";
                case RoomAlert.Emp: return "EMP";
                case RoomAlert.Damaged: return "DAMAGED";
                case RoomAlert.NoPower: return "NO POWER";
                default: return "LOW O2";
            }
        }

        public static Color AlertColour(RoomAlert a)
        {
            switch (a)
            {
                case RoomAlert.Fire: return new Color(1f, 0.45f, 0.1f);
                case RoomAlert.Breach: return new Color(0.3f, 0.65f, 0.95f);
                case RoomAlert.Intruders: return new Color(0.95f, 0.2f, 0.2f);
                case RoomAlert.Emp: return new Color(0.75f, 0.5f, 1f);
                case RoomAlert.Damaged: return new Color(0.95f, 0.75f, 0.2f);
                case RoomAlert.NoPower: return new Color(0.6f, 0.6f, 0.6f);
                default: return new Color(0.45f, 0.85f, 0.95f);
            }
        }

        //Crew (player side, alive) in a room
        public int CrewIn(ShipRoom room)
        {
            int n = 0;
            if (room == null) return 0;
            foreach (var o in room.Occupants)
                if (o != null && ShipCharacters.IsPlayerSide(o) && ShipCharacters.IsAlive(o)) n++;
            return n;
        }

        ShipRoom RandomRoom()
        {
            var rooms = ShipRoom.All;
            return rooms.Count > 0 ? rooms[Random.Range(0, rooms.Count)] : null;
        }

        //What a shot does in the room it lands in; returns the extra words for the log
        string RoomHitEffects(ShipRoom room)
        {
            string msg = "";
            RoomHit?.Invoke(room);
            var s = StationIn(room);
            if (s.HasValue && !damaged.Contains(s.Value) && Random.value < roomStationHitChance)
            {
                damaged.Add(s.Value);
                ClampPower();
                msg += " The " + s.Value + " station is damaged!";
            }
            if (Random.value < fireChance && room.Oxygen >= fireNeedsOxygen && !fires.ContainsKey(room))
            {
                fires[room] = 0.3f;
                msg += " Fire!";
            }
            if (Random.value < breachChance && !breaches.ContainsKey(room))
            {
                breaches[room] = breachSeconds;
                room.VentOxygen();
                msg += " Hull breach!";
            }
            if (Random.value < empChance)
            {
                emps[room] = empSeconds;
                msg += " EMP strike" + (s.HasValue ? ": " + s.Value + " is offline" : "") + "!";
            }
            return msg;
        }

        public void StartFire(ShipRoom room, float size)
        {
            if (room == null || room.Oxygen < fireNeedsOxygen) return;
            fires[room] = Mathf.Max(FireLevel(room), size);
        }

        void RoomsUpdate(float dt)
        {
            //EMP wears off
            foreach (var room in new List<ShipRoom>(emps.Keys))
            {
                if (room == null) { emps.Remove(room); continue; }
                emps[room] -= dt;
                if (emps[room] > 0f) continue;
                emps.Remove(room);
                Log("The " + room.roomName + " is back online after the EMP.");
            }

            Fires(dt);

            //Crew in a breached room patch it faster (breaches tick down in Update; this adds to it)
            foreach (var room in new List<ShipRoom>(breaches.Keys))
            {
                int crew = CrewIn(room);
                if (room != null && crew > 0) breaches[room] -= crewPatchBonus * crew * dt;
            }

            //Crew in a damaged station's room repair it by hand
            foreach (var s in new List<Station>(damaged))
            {
                int crew = CrewIn(RoomOf(s));
                if (crew == 0) continue;
                handRepair.TryGetValue(s, out float p);
                p += crew * dt / handRepairSeconds;
                if (p < 1f) { handRepair[s] = p; continue; }
                handRepair.Remove(s);
                damaged.Remove(s);
                if (Repairing == s) { Repairing = null; repairProgress = 0f; }
                ClampPower();
                Log("The crew in the " + RoomOf(s).roomName + " got the " + s + " station working again.");
            }

            //Life support: the air in every room is topped up only while Ship Systems works
            bool online = LifeSupportOnline;
            foreach (var room in ShipRoom.All) if (room != null) room.LifeSupport = online;
            if (online != lifeSupportWas)
            {
                lifeSupportWas = online;
                Log(online ? "Life support is back on." : "Life support is down! The air is running out.");
            }
        }

        void Fires(float dt)
        {
            foreach (var room in new List<ShipRoom>(fires.Keys))
            {
                if (room == null) { fires.Remove(room); continue; }
                if (room.Oxygen < fireNeedsOxygen)
                {
                    fires.Remove(room); fireBurn.Remove(room);
                    Log("The fire in the " + room.roomName + " dies without air.");
                    continue;
                }
                int crew = CrewIn(room);
                float level = Mathf.Min(1f, fires[room] + (fireGrowth - firefightRate * crew) * dt);
                if (level <= 0f)
                {
                    fires.Remove(room); fireBurn.Remove(room);
                    Log("The fire in the " + room.roomName + " is out.");
                    continue;
                }
                fires[room] = level;

                //It burns everyone inside
                foreach (var o in room.Occupants)
                {
                    if (o == null || !ShipCharacters.IsAlive(o)) continue;
                    var h = o.GetComponent<Health>();
                    if (h != null) h.TakeEnvironmentalDamage(fireDamage * level * dt);
                }

                //Left burning, it wrecks the room's station
                var s = StationIn(room);
                if (level >= 0.5f && s.HasValue && !damaged.Contains(s.Value))
                {
                    fireBurn.TryGetValue(room, out float burn);
                    burn += dt;
                    fireBurn[room] = burn;
                    if (burn >= fireWrecksAfter)
                    {
                        fireBurn[room] = 0f;
                        damaged.Add(s.Value);
                        ClampPower();
                        Log("The fire in the " + room.roomName + " wrecks the " + s.Value + " station!");
                    }
                }

                //A big fire spreads through open doors
                if (level < fireSpreadAt) continue;
                foreach (var door in room.Doors)
                {
                    if (door == null || door.State != ShipDoor.DoorState.Open) continue;
                    foreach (var other in ShipRoom.All)
                    {
                        if (other == null || other == room || fires.ContainsKey(other)) continue;
                        bool shares = false;
                        foreach (var d in other.Doors) if (d == door) { shares = true; break; }
                        if (!shares || Random.value >= fireSpreadChance * dt) continue;
                        StartFire(other, 0.2f);
                        Log("The fire spreads to the " + other.roomName + "!");
                    }
                }
            }
        }

        //"Hold, Engine" for the battle bar
        public string FireSummary
        {
            get
            {
                if (fires.Count == 0) return null;
                var parts = new List<string>();
                foreach (var room in fires.Keys) if (room != null) parts.Add(room.roomName);
                return string.Join(", ", parts);
            }
        }

        // ---------- Orders from the Ship Systems screen ----------

        //Sends the nearest crew member who isn't posted at a station (or failing that, anyone but you) into the room
        public GameObject SendCrew(ShipRoom room)
        {
            if (room == null) return null;
            var box = room.GetComponent<BoxCollider>();
            Vector3 target = box != null ? box.bounds.center : room.transform.position;
            target.y = room.transform.position.y;
            GameObject best = null; float bestScore = float.MaxValue;
            foreach (var c in GameObject.FindGameObjectsWithTag("Companion"))
            {
                if (!ShipCharacters.IsAlive(c)) continue;
                var brain = c.GetComponent<CompanionBrain>();
                if (brain == null || brain.IsControlled) continue;
                if (room.Contains(c.transform.position + Vector3.up * 0.5f)) continue; //Already there
                float score = Vector3.Distance(c.transform.position, target) + (brain.PostName != null ? 1000f : 0f);
                if (score < bestScore) { bestScore = score; best = c; }
            }
            if (best == null) return null;
            if (UnityEngine.AI.NavMesh.SamplePosition(target, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) target = hit.position;
            best.GetComponent<CompanionBrain>().OrderMove(target);
            Log(best.name + " heads for the " + room.roomName + ".");
            return best;
        }
    }
}
