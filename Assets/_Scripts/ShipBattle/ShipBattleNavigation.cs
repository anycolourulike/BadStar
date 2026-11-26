using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Crew.Battle
{
    //What Navigation flies: circle the enemy at range, close in, keep away, stop dead, or head for a point on the map
    public enum Course { Orbit, CloseIn, KeepAway, Stop, Waypoint }
    //How hard: faster is harder to hit but burns more fuel
    public enum Throttle { Slow, Cruise, Full }

    //Steering on the tactical map (the ShipBattle partial). Whoever is at Navigation flies the course and throttle
    //set on the Navigation screen (tap the map for a course point, or pick Orbit / Close in / Keep away / Stop).
    //The ship's speed is its evasion, and fuel burns faster the harder it's pushed. EVADE is a hard turn and burn:
    //it costs fuel, then needs time to come round again; timed while a missile is closing, it gives a good chance to
    //slip a shot that would have hit. Better pilots dodge more and come round sooner. Crew at Navigation (not you)
    //keep flying the last orders and throw the ship about when they see a missile coming, if they're quick enough.
    public partial class ShipBattle
    {
        [Header("Navigation (tactical map)")]
        [Tooltip("Range held when closing in (map units).")]
        [SerializeField] float closeRange = 150f;
        [Tooltip("Range held when keeping away (map units); inside weapon range.")]
        [SerializeField] float awayRange = 600f;
        [Tooltip("Radius flown around a course point once it's reached.")]
        [SerializeField] float holdRadius = 60f;
        [Tooltip("Share of top speed for Slow, Cruise, Full.")]
        [SerializeField] float[] throttleShares = { 0.4f, 0.7f, 1f };
        [Tooltip("How long a hard evasive burn lasts.")]
        [SerializeField] float evadeSeconds = 2.5f;
        [Tooltip("Seconds before the ship can evade again, at skill 0 (each skill point takes 0.6 s off, down to 5 s).")]
        [SerializeField] float evadeCooldown = 14f;
        [SerializeField] float evadeFuel = 3f;
        [Tooltip("Chance a shot that would hit is dodged during the burn, at skill 0.")]
        [SerializeField] float evadeDodge = 0.35f;
        [SerializeField] float evadeDodgePerSkill = 0.03f;
        [Tooltip("Turn rate and acceleration multiplier during the burn.")]
        [SerializeField] float evadeTurnBoost = 3f;
        [Tooltip("Top speed multiplier during the burn.")]
        [SerializeField] float evadeSpeedBoost = 1.3f;
        [Tooltip("How close (map units) a missile gets before a crew pilot reacts to it.")]
        [SerializeField] float pilotReactRange = 110f;

        readonly List<TacticalMissile> incoming = new List<TacticalMissile>();
        readonly HashSet<TacticalMissile> pilotSeen = new HashSet<TacticalMissile>();
        Vector3 evadeDir;

        public Course Course { get; private set; } = Course.Orbit;
        public Throttle Throttle { get; private set; } = Throttle.Cruise;
        public Vector3 Waypoint { get; private set; }
        public bool WaypointReached { get; private set; }
        public float EvadeLeft { get; private set; }
        public float EvadeCooldownLeft { get; private set; }
        public bool Evading => EvadeLeft > 0f;
        public IReadOnlyList<TacticalMissile> Incoming => incoming;

        public int PilotSkill { get { var t = Terminal(Station.Navigation); return t != null ? t.OperatorSkill : 0; } }
        //Someone (or the autopilot) can fly the ship: fuel in the tanks and Navigation working
        public bool CanSteer => HasTacticalMap && Fuel > 0f && CrewFactor(Station.Navigation) > 0f;
        public float ThrottleShare => throttleShares != null && throttleShares.Length > (int)Throttle ? throttleShares[(int)Throttle] : 0.7f;
        public float EvadeChance => Mathf.Min(0.75f, evadeDodge + evadeDodgePerSkill * PilotSkill);
        public float EvadeCooldownTime => Mathf.Max(5f, evadeCooldown - 0.6f * PilotSkill);
        public float EvadeCharge => EvadeCooldownLeft <= 0f ? 1f : 1f - EvadeCooldownLeft / Mathf.Max(0.1f, EvadeCooldownTime + evadeSeconds);
        public float CloseRange => closeRange;
        public float AwayRange => awayRange;
        public float OrbitRange => playerRange;

        //Why the ship can't evade now (null when it can)
        public string EvadeNotReady
        {
            get
            {
                if (State != BattleState.Fighting) return "battle over";
                if (!HasTacticalMap) return "no map";
                if (Fuel < evadeFuel) return "no fuel";
                if (!CanSteer) return IsDamaged(Station.Navigation) ? "navigation damaged" : "no helm";
                if (Evading) return "evading";
                if (EvadeCooldownLeft > 0f) return "ready in " + Mathf.CeilToInt(EvadeCooldownLeft) + "s";
                return null;
            }
        }

        //The nearest missile flying at us, and how long until it lands (seconds)
        public TacticalMissile NearestIncoming(out float distance, out float eta)
        {
            TacticalMissile best = null;
            distance = eta = Mathf.Infinity;
            if (!HasTacticalMap) return null;
            foreach (var m in incoming)
            {
                if (m == null) continue;
                float d = Flat(m.transform.position - PlayerShip.Position).magnitude;
                if (d >= distance) continue;
                best = m; distance = d;
                eta = d / Mathf.Max(5f, m.currentSpeed);
            }
            return best;
        }

        // ---------- Orders from the Navigation screen ----------

        public void SetCourse(Course c)
        {
            if (State != BattleState.Fighting || c == Course.Waypoint) return;
            Course = c;
            Log("Helm: " + CourseName(c) + ".");
            Changed?.Invoke();
        }

        //Fly to a point on the map, then circle it
        public void SetWaypoint(Vector3 point)
        {
            if (State != BattleState.Fighting || !HasTacticalMap) return;
            point.y = PlayerShip.Position.y;
            Waypoint = point;
            WaypointReached = false;
            Course = Course.Waypoint;
            Changed?.Invoke();
        }

        public void SetThrottle(Throttle t)
        {
            if (State != BattleState.Fighting) return;
            Throttle = t;
            Changed?.Invoke();
        }

        public bool StartEvade()
        {
            if (EvadeNotReady != null) return false;
            var me = PlayerShip;
            //Swerve across the line of the nearest threat (the enemy if nothing is flying at us)
            var m = NearestIncoming(out _, out _);
            Vector3 from = Flat(me.Position - (m != null ? m.transform.position : EnemyShip.Position));
            Vector3 side = Vector3.Cross(Vector3.up, from.sqrMagnitude > 0.01f ? from.normalized : me.Heading);
            if (Vector3.Dot(side, me.Heading) < 0f) side = -side;
            evadeDir = (side + me.Heading * 0.3f).normalized;
            EvadeLeft = evadeSeconds;
            EvadeCooldownLeft = EvadeCooldownTime + evadeSeconds;
            Fuel = Mathf.Max(0f, Fuel - evadeFuel);
            Log("Evasive manoeuvre!");
            Changed?.Invoke();
            return true;
        }

        public static string CourseName(Course c)
        {
            switch (c)
            {
                case Course.Orbit: return "orbit";
                case Course.CloseIn: return "close in";
                case Course.KeepAway: return "keep away";
                case Course.Stop: return "all stop";
                default: return "course point";
            }
        }

        // ---------- Flying ----------

        //Called from Manoeuvre: our ship's course, speed, the evasive burn and the fuel it all costs
        void FlyPlayer(float dt)
        {
            var me = space.playerShip;
            var foe = space.enemyShip;
            incoming.RemoveAll(x => x == null);
            pilotSeen.RemoveWhere(x => x == null);
            if (EvadeLeft > 0f) EvadeLeft = Mathf.Max(0f, EvadeLeft - dt);
            if (EvadeCooldownLeft > 0f) EvadeCooldownLeft = Mathf.Max(0f, EvadeCooldownLeft - dt);
            if (IsManned(Station.Navigation) && !IsPlayerControlled(Station.Navigation)) AutoPilot();

            //Damaged, taken or dry, the ship drifts to a stop on its last heading
            float want = 0f, boost = 1f;
            Vector3 dir = me.Heading;
            if (CanSteer)
            {
                //Low power or a poor pilot can't get all the throttle asks for
                float helm = Mathf.Clamp01(0.5f + 0.5f * Effect(Station.Navigation));
                want = me.maxSpeed * ThrottleShare * helm;
                if (Escaping) { dir = me.Position - foe.Position; want = me.maxSpeed * helm; }
                else switch (Course)
                {
                    case Course.Orbit: dir = Orbit(me.Position, foe.Position, playerRange, 1f); break;
                    case Course.CloseIn: dir = Orbit(me.Position, foe.Position, closeRange, 1f); break;
                    case Course.KeepAway: dir = Orbit(me.Position, foe.Position, awayRange, 1f); break;
                    case Course.Stop: want = 0f; break;
                    case Course.Waypoint:
                        Vector3 to = Flat(Waypoint - me.Position);
                        if (!WaypointReached && to.magnitude < holdRadius) { WaypointReached = true; Log("Helm: at the course point, circling."); }
                        dir = WaypointReached ? Orbit(me.Position, Waypoint, holdRadius, 1f) : to;
                        break;
                }
                if (Evading) { dir = evadeDir; want = me.maxSpeed * evadeSpeedBoost; boost = evadeTurnBoost; }
            }
            me.Drive(dir, want, dt, boost);

            //Pushing harder costs more than it gains; the engineer makes the fuel go further
            float share = me.SpeedShare;
            Fuel = Mathf.Max(0f, Fuel - fuelBurn * share * share * 1.25f * dt / Mathf.Max(0.5f, Effect(Station.Engines)));
            if (Fuel <= 0f && !fuelWarned) { fuelWarned = true; Log("<color=#E64033>Out of fuel! The ship is drifting.</color>"); }
        }

        //A crew pilot keeps the last orders, eases off when fuel runs low, and throws the ship about when a missile
        //comes close (quicker pilots catch more of them)
        void AutoPilot()
        {
            if (Throttle == Throttle.Full && Fuel < maxFuel * 0.2f && !Escaping) Throttle = Throttle.Cruise;
            foreach (var m in incoming)
            {
                if (m == null || pilotSeen.Contains(m)) continue;
                if (Flat(m.transform.position - PlayerShip.Position).magnitude > pilotReactRange) continue;
                pilotSeen.Add(m);
                if (EvadeNotReady == null && Random.value < 0.3f + 0.06f * PilotSkill) StartEvade();
            }
        }

        //An incoming shot that would hit can still be slipped by a hard burn at the right moment
        bool DodgedByEvade(Shot shot)
        {
            if (!shot.incoming || !shot.hit || !Evading) return false;
            return Random.value < EvadeChance;
        }
    }
}
