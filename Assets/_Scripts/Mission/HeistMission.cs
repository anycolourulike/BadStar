using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

namespace Rambler.Mission
{
    //The drone-strike plan, one step per squad member:
    //  Pervis distracts the gate guards  >  Hayes cuts the power  >  Rambler slips into the workshop before the backup
    //  generators  >  Rambler hacks the prototype drone  >  Hayes takes the drone link at the relay  >  Rambler opens
    //  the workshop door from inside  >  fly the drone to the meeting and strike  >  everyone to extraction.
    //Wires the pieces together, sets the squad's standing orders and shows the plan on screen. Going loud raises the
    //alarm (weapons free, guards hunt) but the plan can still be finished.
    public class HeistMission : MonoBehaviour
    {
        [Header("Squad (by name)")]
        [SerializeField] string distractorName = "Pervis";
        [SerializeField] string saboteurName = "Hayes";
        [SerializeField] string infiltratorName = "Rambler";

        [Header("Pieces")]
        [SerializeField] MissionInteractable breaker;
        [SerializeField] MissionInteractable hackPanel;
        [SerializeField] MissionInteractable innerPanel;
        [SerializeField] MissionInteractable relay;
        [SerializeField] PilotDrone drone;
        [SerializeField] PoweredLock workshopDoor;
        [SerializeField] BoxCollider workshopInside;
        [SerializeField] MissionTarget target;
        [SerializeField] Transform extraction;
        [SerializeField] float extractionRadius = 4f;
        [SerializeField] float doorOpenSeconds = 20f;
        [Tooltip("Switched off when the mission starts (e.g. the Sandbox's wave enemies).")]
        [SerializeField] GameObject[] disableOnStart;

        PlayerSelector selector;
        GameObject pervis, hayes, rambler;
        Distractor distractor;
        readonly bool[] done = new bool[8];
        static readonly string[] steps =
        {
            "Pervis: get the gate guards talking",
            "Hayes: cut the power at the substation",
            "Rambler: get inside the workshop before the backup generators",
            "Rambler: hack the prototype drone",
            "Hayes: take the drone link at the relay",
            "Rambler: open the workshop door from inside",
            "Fly the drone to the meeting and strike the target",
            "Everyone back to the extraction point",
        };
        bool complete;

        void Start()
        {
            foreach (var g in disableOnStart) if (g != null) g.SetActive(false);
            SquadOrders.HoldPosition = true;
            SquadOrders.HoldFire = true;
            SquadOrders.Alarm = false;
            selector = FindObjectOfType<PlayerSelector>();
            pervis = Member(distractorName); hayes = Member(saboteurName); rambler = Member(infiltratorName);
            if (pervis != null)
            {
                if (pervis.GetComponent<Disguise>() == null) pervis.AddComponent<Disguise>();
                distractor = pervis.GetComponent<Distractor>() ?? pervis.AddComponent<Distractor>();
            }
            hackPanel.available = false; innerPanel.available = false; relay.available = false;
            breaker.onUsed.AddListener(() => { if (PowerGrid.Instance != null) PowerGrid.Instance.CutPower(breaker.transform.position); });
            hackPanel.onUsed.AddListener(() => { drone.Compromise(); relay.available = true; });
            relay.onUsed.AddListener(() => drone.TakeControl());
            innerPanel.onUsed.AddListener(() => workshopDoor.Unlock(doorOpenSeconds));
            Debug.Log("[Mission] heist started: pervis=" + (pervis != null) + " hayes=" + (hayes != null) + " rambler=" + (rambler != null));
        }

        GameObject Member(string n)
        {
            if (selector == null) return null;
            foreach (var m in selector.players) if (m != null && m.name.StartsWith(n)) return m;
            return null;
        }

        bool Inside(GameObject who) => who != null && workshopInside != null && workshopInside.bounds.Contains(who.transform.position + Vector3.up * 0.5f);

        void Update()
        {
            if (complete) return;
            //Anyone seen raises the alarm: the squad is weapons free and the guards hunt
            if (!SquadOrders.Alarm)
                foreach (var g in FindObjectsOfType<EnemyAI>())
                    if (!g.IsDead && g.TargetVisible) { SquadOrders.Alarm = true; Debug.Log("[Mission] ALARM: " + g.name + " saw " + (g.Target != null ? g.Target.name : "someone")); break; }

            if (!done[0] && distractor != null && distractor.Active) done[0] = true;
            if (!done[1] && breaker.Used) done[1] = true;
            if (!done[2] && Inside(rambler)) done[2] = true;
            hackPanel.available = Inside(rambler);
            innerPanel.available = done[3] && Inside(rambler);
            if (!done[3] && hackPanel.Used) done[3] = true;
            if (!done[4] && drone.Current >= PilotDrone.State.Piloted) done[4] = true;
            if (!done[5] && innerPanel.Used) done[5] = true;
            if (!done[6] && target != null && target.IsDead) done[6] = true;
            if (done[6] && !done[7] && SquadHome()) { done[7] = true; complete = true; Debug.Log("[Mission] complete" + (SquadOrders.Alarm ? " (alarm raised)" : " (clean)")); }
        }

        bool SquadHome()
        {
            foreach (var m in selector.players)
            {
                if (m == null) continue;
                var h = m.GetComponent<Rambler.Attributes.Health>();
                if (h != null && h.IsDead()) continue;
                Vector3 d = m.transform.position - extraction.position; d.y = 0f;
                if (d.magnitude > extractionRadius) return false;
            }
            return true;
        }

        GUIStyle box, line;
        void OnGUI()
        {
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(Screen.height * 0.024f), wordWrap = true };
                line = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.024f), wordWrap = true };
            }
            float w = Screen.width * 0.34f, lh = Screen.height * 0.034f;
            float x = Screen.width * 0.01f, y = Screen.height * 0.2f;
            GUI.Box(new Rect(x, y, w, lh * (steps.Length + 2.2f)), SquadOrders.Alarm ? "PLAN - ALARM RAISED" : "PLAN", box);
            for (int i = 0; i < steps.Length; i++)
            {
                line.normal.textColor = done[i] ? new Color(0.5f, 1f, 0.5f) : Color.white;
                string extra = i == 0 && distractor != null && distractor.Active ? "  (" + distractor.TimeLeft.ToString("0") + "s)" : "";
                GUI.Label(new Rect(x + 8, y + lh * (i + 1.1f), w - 12, lh), (done[i] ? "[x] " : "[ ] ") + steps[i] + extra, line);
            }
            if (complete)
            {
                line.normal.textColor = Color.yellow;
                GUI.Label(new Rect(x + 8, y + lh * (steps.Length + 1.1f), w - 12, lh), SquadOrders.Alarm ? "MISSION COMPLETE (messy)" : "MISSION COMPLETE - CLEAN", line);
            }
        }

        void OnDrawGizmos()
        {
            if (extraction == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(extraction.position, extractionRadius);
        }
    }
}
