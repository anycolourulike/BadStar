using UnityEngine;
using Rambler.Attributes;
using Rambler.Control;

namespace Rambler.Mission
{
    //Runs one Sandbox mini scenario: puts the squad at its start spots, sets the squad's standing orders, gives Pervis
    //his visitor pass if needed, and tracks a single goal (reach a zone / defeat everyone / kill the target), shown
    //on screen with pass or fail. Put it on the scenario's root; ScenarioSwitcher switches the roots on and off.
    public class ScenarioDirector : MonoBehaviour
    {
        public enum Goal { ReachZone, DefeatAll, KillTarget, None }

        [SerializeField] string title = "Scenario";
        [TextArea] [SerializeField] string brief = "";

        [Header("Squad")]
        [Tooltip("Start spots, matched to squad members by name (a spot named \"Hayes\" puts Hayes there).")]
        [SerializeField] Transform[] squadStarts;
        [SerializeField] string startControlling = "Rambler";
        [SerializeField] bool holdPosition;
        [SerializeField] bool holdFire;
        [Tooltip("Pervis wears a visitor pass and distracts calm guards he walks up to.")]
        [SerializeField] bool pervisDistracts;

        [Header("Goal")]
        [SerializeField] Goal goal = Goal.DefeatAll;
        [Tooltip("ReachZone: who has to get there (empty = any squad member).")]
        [SerializeField] string reacher = "Rambler";
        [SerializeField] Transform zone;
        [SerializeField] float zoneRadius = 3f;
        [Tooltip("DefeatAll: the enemies (everything with Health under this). Empty = this scenario.")]
        [SerializeField] Transform enemies;
        [SerializeField] MissionTarget target;
        [Tooltip("Getting seen fails the scenario (stealth ones).")]
        [SerializeField] bool failOnAlarm;
        [Tooltip("The enemies know where the squad is from the start (they come looking): firefight drills.")]
        [SerializeField] bool alertEnemiesAtStart;

        [Header("Extras")]
        [Tooltip("Drones that start already hacked (the drone strike drill).")]
        [SerializeField] PilotDrone[] precompromised;
        [Tooltip("Pairs: interactable -> drone it links to (TakeControl).")]
        [SerializeField] MissionInteractable droneLink;
        [SerializeField] PilotDrone linkedDrone;

        PlayerSelector selector;
        bool passed, failed;
        float started;

        void Start()
        {
            started = Time.time;
            selector = FindObjectOfType<PlayerSelector>();
            SquadOrders.HoldPosition = holdPosition;
            SquadOrders.HoldFire = holdFire;
            SquadOrders.Alarm = false;
            PlaceSquad();
            Invoke(nameof(PlaceSquad), 0.6f); //Again once the level's own set-up (spawn points, companions) has run
            foreach (var d in precompromised) if (d != null) d.Compromise();
            if (droneLink != null && linkedDrone != null) droneLink.onUsed.AddListener(() => linkedDrone.TakeControl());
            if (enemies == null) enemies = transform;
        }

        void PlaceSquad()
        {
            if (selector != null)
            {
                foreach (var m in selector.players)
                {
                    if (m == null) continue;
                    foreach (var s in squadStarts)
                    {
                        if (s == null || !m.name.StartsWith(s.name)) continue;
                        var agent = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null && agent.enabled) agent.Warp(s.position); else m.transform.position = s.position;
                        m.transform.rotation = s.rotation;
                    }
                    if (pervisDistracts && m.name.StartsWith("Pervis"))
                    {
                        if (m.GetComponent<Disguise>() == null) m.AddComponent<Disguise>();
                        if (m.GetComponent<Distractor>() == null) m.AddComponent<Distractor>();
                    }
                    if (m.name.StartsWith(startControlling) && selector.activePlayer != m) selector.SwitchTo(m);
                }
            }
            if (alertEnemiesAtStart && selector != null && selector.activePlayer != null)
                foreach (var g in GetComponentsInChildren<EnemyAI>()) g.HearOf(selector.activePlayer.transform.position);
        }

        void Update()
        {
            if (passed || failed) return;
            //The squad shooting first, or being seen, is the alarm: weapons free from then on
            if (!SquadOrders.Alarm)
            {
                foreach (var g in GetComponentsInChildren<EnemyAI>())
                    if (!g.IsDead && g.TargetVisible) { RaiseAlarm(g.name + " spotted " + (g.Target != null ? g.Target.name : "someone")); break; }
                var me = selector != null ? selector.activePlayer : null;
                var f = me != null ? me.GetComponent<Rambler.Combat.Fighter>() : null;
                if (!SquadOrders.Alarm && f != null && f.EnemyPos != null) RaiseAlarm(me.name + " opened fire");
            }
            if (failOnAlarm && SquadOrders.Alarm) { failed = true; return; }
            switch (goal)
            {
                case Goal.ReachZone: if (Reached()) passed = true; break;
                case Goal.DefeatAll: if (AllDown()) passed = true; break;
                case Goal.KillTarget: if (target != null && target.IsDead) passed = true; break;
            }
            if (SquadDown()) failed = true;
        }

        void RaiseAlarm(string why)
        {
            SquadOrders.Alarm = true;
            Debug.Log("[Scenario] alarm: " + why);
        }

        bool Reached()
        {
            if (zone == null || selector == null) return false;
            foreach (var m in selector.players)
            {
                if (m == null || (reacher != "" && !m.name.StartsWith(reacher))) continue;
                Vector3 d = m.transform.position - zone.position; d.y = 0f;
                if (d.magnitude <= zoneRadius) return true;
            }
            return false;
        }

        bool AllDown()
        {
            int alive = 0;
            foreach (var h in enemies.GetComponentsInChildren<Health>()) if (h.CompareTag("Enemy") && !h.IsDead()) alive++;
            return alive == 0 && Time.time - started > 2f;
        }

        bool SquadDown()
        {
            if (selector == null) return false;
            foreach (var m in selector.players)
            {
                if (m == null) continue;
                var h = m.GetComponent<Health>();
                if (h == null || !h.IsDead()) return false;
            }
            return true;
        }

        GUIStyle box, big;
        void OnGUI()
        {
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = Mathf.RoundToInt(Screen.height * 0.024f) };
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(Screen.height * 0.06f) };
            }
            float w = Screen.width * 0.34f;
            string status = passed ? "PASSED" : failed ? "FAILED" : SquadOrders.Alarm ? "ALARM - weapons free" : (holdFire ? "Stealth - hold fire" : "Weapons free");
            GUI.Box(new Rect(Screen.width * 0.01f, Screen.height * 0.2f, w, Screen.height * 0.22f), title.ToUpper() + "\n" + brief + "\n\n" + status, box);
            if (passed || failed)
            {
                big.normal.textColor = passed ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.3f);
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, Screen.height * 0.12f), passed ? "SCENARIO PASSED" : "SCENARIO FAILED", big);
            }
        }

        void OnDrawGizmos()
        {
            if (goal != Goal.ReachZone || zone == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.6f);
            Gizmos.DrawWireSphere(zone.position, zoneRadius);
        }
    }
}
