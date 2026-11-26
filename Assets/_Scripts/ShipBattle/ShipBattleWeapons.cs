using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Rambler.Crew.Battle
{
    public enum WeaponKind { Missile, Rocket }

    //One of the ship's weapons: its ammunition, reload and how well it hits
    [Serializable]
    public class Launcher
    {
        public WeaponKind kind = WeaponKind.Missile;
        public string name = "Missiles";
        public int rounds = 8;
        [Tooltip("Projectiles per shot (a rocket salvo fires several).")]
        public int salvo = 1;
        [Tooltip("Seconds to reload at 100% Weapons.")]
        public float reloadSeconds = 10f;
        [Tooltip("Damage per projectile at firepower 70 (the Shearwater).")]
        public int damage = 45;
        [Range(0f, 1f)] public float accuracy = 0.85f;
        [Tooltip("Hit chance lost against a target at full speed.")]
        [Range(0f, 1f)] public float speedPenalty = 0.25f;

        [NonSerialized] public float charge;
        [NonSerialized] public int left;
        public bool Charged => charge >= 1f;
    }

    //A part of the enemy ship you can aim at on the target picture
    [Serializable]
    public class EnemyRoom
    {
        public string name = "Engines";
        public EnemyTarget system = EnemyTarget.Engines;
        [Tooltip("Where it is on the target picture (0-1, nose to the right).")]
        public Rect area = new Rect(0f, 0f, 0.25f, 1f);
        [Tooltip("Weapons skill (trait included) needed to aim at it.")]
        public int minSkill = 3;
    }

    //The ship-to-ship fight on the tactical map (the ShipBattle partial). Both ships are lights moving over the Titan
    //surface: Navigation keeps us circling at range (faster = harder to hit, more fuel), the enemy circles the other
    //way. The Weapons station scans the enemy (its rooms and systems show on the target picture once scanned), then
    //fires missiles and rockets at the hull or at a room. Each shot's chance falls with the target's speed and range
    //and rises with the gunner's skill; aiming at a room is harder than the hull, and an unscanned ship can only be hit
    //in the hull. A better gunner can pick out more rooms. Projectiles fly on the map (Missile and Rocket Behavior)
    //and hit or miss when they arrive. The enemy's volleys fly at us the same way. Fuel is burnt by speed; a good
    //engineer at Engines makes it go further; out of fuel, the ship drifts.
    public partial class ShipBattle
    {
        //A projectile in flight and what it does when it arrives
        class Shot
        {
            public bool incoming;
            public bool hit;
            public int damage;
            public EnemyTarget target;
        }

        [Header("Weapons (tactical map)")]
        [SerializeField] Launcher[] launchers =
        {
            new Launcher(),
            new Launcher { kind = WeaponKind.Rocket, name = "Rockets", rounds = 24, salvo = 3, reloadSeconds = 6f, damage = 14, accuracy = 0.72f, speedPenalty = 0.4f },
        };
        [SerializeField] float scanSeconds = 6f;
        [Tooltip("Map units; beyond this nothing can be fired.")]
        [SerializeField] float weaponRange = 700f;
        [Tooltip("Hit chance each point of the gunner's skill adds.")]
        [SerializeField] float skillAccuracy = 0.025f;
        [Tooltip("Hit chance lost aiming at a room instead of the hull.")]
        [SerializeField] float roomAimPenalty = 0.1f;
        [Tooltip("Hit chance lost before the target is scanned.")]
        [SerializeField] float unscannedPenalty = 0.15f;
        [SerializeField] EnemyRoom[] enemyRooms =
        {
            new EnemyRoom { name = "Engines", system = EnemyTarget.Engines, area = new Rect(0f, 0.15f, 0.24f, 0.7f), minSkill = 3 },
            new EnemyRoom { name = "Hold", system = EnemyTarget.Hull, area = new Rect(0.24f, 0.1f, 0.32f, 0.8f), minSkill = 0 },
            new EnemyRoom { name = "Weapons", system = EnemyTarget.Weapons, area = new Rect(0.56f, 0.1f, 0.22f, 0.8f), minSkill = 3 },
            new EnemyRoom { name = "Bridge", system = EnemyTarget.Bridge, area = new Rect(0.78f, 0.2f, 0.22f, 0.6f), minSkill = 6 },
        };

        [Header("Movement and fuel")]
        [Tooltip("Distance Navigation keeps from the enemy (map units).")]
        [SerializeField] float playerRange = 320f;
        [SerializeField] float enemyRange = 260f;
        [SerializeField] float maxFuel = 100f;
        [Tooltip("Fuel used a second at full speed, with Engines at 100%.")]
        [SerializeField] float fuelBurn = 0.12f;

        TacticalSpace space;
        float bridgeDown;
        bool fuelWarned;

        public bool HasTacticalMap => space != null && space.playerShip != null && space.enemyShip != null;
        public TacticalShip PlayerShip => space != null ? space.playerShip : null;
        public TacticalShip EnemyShip => space != null ? space.enemyShip : null;
        public TacticalSpace Space => space;
        public float Fuel { get; private set; }
        public float MaxFuel => maxFuel;
        public bool Scanned { get; private set; }
        public bool Scanning { get; private set; }
        public float ScanProgress { get; private set; }
        public IReadOnlyList<Launcher> Launchers => launchers;
        public IReadOnlyList<EnemyRoom> EnemyRooms => enemyRooms;
        public int EnemyBridge { get; private set; }
        public bool EnemyBridgeDown => bridgeDown > 0f;
        public float WeaponRange => weaponRange;
        public float Range => HasTacticalMap ? Vector3.Distance(space.playerShip.Position, space.enemyShip.Position) : weaponRange * 0.5f;
        public int GunnerSkill { get { var t = Terminal(Station.Weapons); return t != null ? t.OperatorSkill : 0; } }

        void WeaponsStart()
        {
            space = TacticalSpace.Instance != null ? TacticalSpace.Instance : FindObjectOfType<TacticalSpace>();
            Fuel = maxFuel;
            EnemyBridge = enemySystemHealth;
            foreach (var l in launchers) { l.left = l.rounds; l.charge = 0.5f; }
            if (space != null && space.enemyShip != null) space.enemyShip.DisplayName = enemyName;
            if (space != null && space.playerShip != null) space.playerShip.DisplayName = ShipName;
        }

        void WeaponsUpdate(float dt)
        {
            bool manned = IsManned(Station.Weapons);
            float eff = Effect(Station.Weapons);
            foreach (var l in launchers)
                if (manned && l.left > 0) l.charge = Mathf.Min(1f, l.charge + eff * dt / Mathf.Max(0.5f, l.reloadSeconds));

            if (Scanning && manned)
            {
                ScanProgress = Mathf.Min(1f, ScanProgress + CrewFactor(Station.Weapons) * dt / Mathf.Max(0.5f, scanSeconds));
                if (ScanProgress >= 1f)
                {
                    Scanning = false;
                    Scanned = true;
                    Log("Scan complete: the " + enemyName + "'s rooms and systems are on the targeting screen.");
                }
            }

            if (bridgeDown > 0f && (bridgeDown -= dt) <= 0f) { EnemyBridge = enemySystemHealth / 2; Log("The " + enemyName + "'s bridge is back in action."); }

            //Crew at Weapons (not you) scan, then fire whatever is loaded at their chosen target
            if (manned && eff > 0f && !IsPlayerControlled(Station.Weapons)) AutoGunner();

            Manoeuvre(dt);
        }

        // ---------- Scanning and aiming ----------

        public bool StartScan()
        {
            if (Scanned || Scanning || !IsManned(Station.Weapons)) return false;
            Scanning = true;
            ScanProgress = 0f;
            Log("Scanning the " + enemyName + "...");
            return true;
        }

        //Can the current gunner aim at this part of the enemy?
        public bool CanAim(EnemyRoom room) => Scanned ? GunnerSkill >= room.minSkill : room.system == EnemyTarget.Hull;

        //Chance one projectile from this launcher hits this target right now
        public float Chance(Launcher l, EnemyTarget target)
        {
            float c = l.accuracy + skillAccuracy * GunnerSkill;
            var foe = EnemyShip;
            if (foe != null && !EnemyBridgeDown) c -= l.speedPenalty * foe.SpeedShare;
            c -= 0.15f * Mathf.Clamp01(Range / Mathf.Max(1f, weaponRange));
            if (target != EnemyTarget.Hull) c -= roomAimPenalty;
            if (!Scanned) c -= unscannedPenalty;
            return Mathf.Clamp(c, 0.05f, 0.95f);
        }

        //Why this launcher can't fire now (null when it can)
        public string NotReady(Launcher l)
        {
            if (State != BattleState.Fighting) return "battle over";
            if (!IsManned(Station.Weapons) || Effect(Station.Weapons) <= 0f) return IsDamaged(Station.Weapons) ? "weapons damaged" : "no power";
            if (l.left < 1) return "no rounds";
            if (Range > weaponRange) return "out of range";
            if (!l.Charged) return "reloading " + Mathf.CeilToInt((1f - l.charge) * l.reloadSeconds / Mathf.Max(0.1f, Effect(Station.Weapons))) + "s";
            return null;
        }

        public bool FireAt(Launcher l, EnemyTarget target)
        {
            if (l == null || NotReady(l) != null) return false;
            if (!Scanned) target = EnemyTarget.Hull;
            l.charge = 0f;
            int count = Mathf.Min(l.salvo, l.left);
            l.left -= count;
            float chance = Chance(l, target);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(l.damage * firepower / 70f));
            var prefab = space != null ? (l.kind == WeaponKind.Missile ? space.missilePrefab : space.rocketPrefab) : null;
            for (int i = 0; i < count; i++)
            {
                var shot = new Shot { hit = Random.value < chance, damage = dmg, target = target };
                if (!Launch(prefab, PlayerShip, EnemyShip, shot, i)) Resolve(shot); //No map: lands at once
            }
            string what = target == EnemyTarget.Hull ? "hull" : target.ToString().ToLowerInvariant();
            Log((count > 1 ? count + " " + l.name.ToLowerInvariant() : l.name.TrimEnd('s')) + " away at the " + enemyName + "'s " + what + " (" + Mathf.RoundToInt(chance * 100f) + "%).");
            return true;
        }

        void AutoGunner()
        {
            if (!Scanned && !Scanning) StartScan();
            var target = Scanned ? Target : EnemyTarget.Hull;
            //Rockets up close, missiles at range; whatever is loaded
            bool close = Range < weaponRange * 0.5f;
            Launcher pick = null;
            foreach (var l in launchers)
            {
                if (NotReady(l) != null) continue;
                if (pick == null || (close ? l.kind == WeaponKind.Rocket : l.kind == WeaponKind.Missile)) pick = l;
            }
            if (pick != null) FireAt(pick, target);
        }

        // ---------- Projectiles ----------

        //Puts a projectile on the map, flying at the target (or a point beside it if it's going to miss)
        bool Launch(TacticalMissile prefab, TacticalShip from, TacticalShip to, Shot shot, int index)
        {
            if (prefab == null || from == null || to == null) return false;
            Vector3 toTarget = to.Position - from.Position; toTarget.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized);
            Vector3 start = from.Position + toTarget.normalized * 10f + side * (index - 1) * 6f;
            var rot = Quaternion.LookRotation(Quaternion.Euler(0f, Random.Range(-35f, 35f) + (index - 1) * 20f, 0f) * toTarget.normalized, Vector3.up);
            var m = Instantiate(prefab, start, rot, space.transform);
            GameObject aim = to.gameObject;
            if (!shot.hit)
            {
                aim = new GameObject("MissPoint");
                aim.transform.SetParent(to.transform, false);
                aim.transform.localPosition = new Vector3(Random.Range(26f, 44f) * (Random.value < 0.5f ? -1f : 1f), 0f, Random.Range(-30f, 30f));
            }
            m.Payload = shot;
            m.Launch(aim, shot.hit ? 9f : 5f);
            var missPoint = shot.hit ? null : aim;
            m.Arrived = arrived =>
            {
                if (missPoint != null) Destroy(missPoint, 0.1f);
                Resolve(shot);
            };
            return true;
        }

        void Resolve(Shot shot)
        {
            if (State != BattleState.Fighting) return;
            if (shot.incoming)
            {
                if (shot.hit) TakeHit(shot.damage);
                else Log("Evaded a shot!");
                return;
            }
            if (shot.hit) ApplyEnemyDamage(shot.target, shot.damage);
            else Log("Missed the " + enemyName + ".");
        }

        //An enemy shot: flies on the map when there is one. Whether it hits is our evasion (speed) at launch.
        bool SpawnIncoming(int dmg)
        {
            if (!HasTacticalMap || space.enemyRocketPrefab == null) return false;
            var shot = new Shot { incoming = true, damage = dmg, hit = Random.value >= Evade };
            return Launch(space.enemyRocketPrefab, EnemyShip, PlayerShip, shot, 1);
        }

        // ---------- Movement ----------

        void Manoeuvre(float dt)
        {
            if (!HasTacticalMap) return;
            var me = space.playerShip;
            var foe = space.enemyShip;

            //Navigation flies us; damaged or taken, the ship drifts to a stop
            float want = 0f;
            if (Fuel > 0f && CrewFactor(Station.Navigation) > 0f)
                want = Escaping ? me.maxSpeed : me.maxSpeed * Mathf.Clamp01(0.35f + 0.45f * Effect(Station.Navigation));
            Vector3 dir = Escaping ? me.Position - foe.Position : Orbit(me.Position, foe.Position, playerRange, 1f);
            me.Drive(dir, want, dt);

            //The engineer makes the fuel go further
            Fuel = Mathf.Max(0f, Fuel - fuelBurn * me.SpeedShare * dt / Mathf.Max(0.5f, Effect(Station.Engines)));
            if (Fuel <= 0f && !fuelWarned) { fuelWarned = true; Log("<color=#E64033>Out of fuel! The ship is drifting.</color>"); }

            //The enemy circles the other way; engines down, it stops; bridge down, it can't steer
            float enemyWant = EnemyEnginesDown ? 0f : foe.maxSpeed * 0.8f;
            Vector3 enemyDir = EnemyBridgeDown ? foe.Heading : Orbit(foe.Position, me.Position, enemyRange, -1f);
            foe.Drive(enemyDir, enemyWant, dt);

            me.SetGlow(me.SpeedShare);
            foe.SetGlow(EnemyEnginesDown ? 0f : foe.SpeedShare);
        }

        //Circle a point at a set distance: sideways, plus in or out to hold the range
        static Vector3 Orbit(Vector3 me, Vector3 centre, float range, float sign)
        {
            Vector3 to = centre - me; to.y = 0f;
            float d = Mathf.Max(0.01f, to.magnitude);
            Vector3 radial = to / d;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * sign;
            return (tangent + radial * Mathf.Clamp((d - range) / range * 2f, -1f, 1f)).normalized;
        }

        // ---------- Damage to the enemy ----------

        void ApplyEnemyDamage(EnemyTarget target, int dmg)
        {
            switch (target)
            {
                case EnemyTarget.Hull:
                    EnemyHull -= dmg;
                    Log("Hit the " + enemyName + "'s hull for " + dmg + ".");
                    break;
                case EnemyTarget.Weapons:
                    EnemyHull -= dmg / 2;
                    if (!EnemyWeaponsDown && (EnemyWeapons -= dmg) <= 0) { EnemyWeapons = 0; weaponsDown = systemDownSeconds; Log("The " + enemyName + "'s weapons are down!"); }
                    else Log("Hit the " + enemyName + "'s weapons.");
                    break;
                case EnemyTarget.Engines:
                    EnemyHull -= dmg / 2;
                    if (!EnemyEnginesDown && (EnemyEngines -= dmg) <= 0) { EnemyEngines = 0; enginesDown = systemDownSeconds; Log("The " + enemyName + "'s engines are down: it's dead in space, and no more boarders for now!"); }
                    else Log("Hit the " + enemyName + "'s engines.");
                    break;
                case EnemyTarget.Bridge:
                    EnemyHull -= dmg / 2;
                    if (!EnemyBridgeDown && (EnemyBridge -= dmg) <= 0) { EnemyBridge = 0; bridgeDown = systemDownSeconds; Log("The " + enemyName + "'s bridge is knocked out: it can't steer or dodge!"); }
                    else Log("Hit the " + enemyName + "'s bridge.");
                    break;
            }
            if (EnemyHull <= 0) { EnemyHull = 0; End(BattleState.Won); }
        }
    }
}
