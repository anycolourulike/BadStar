using System;
using UnityEngine;
using Rambler.Control;

namespace Rambler.Mission
{
    //The building's power. Cutting the breaker blacks everything out: maglocks release, lights die. After a short
    //spell the backup generators kick in and the locks engage again (the window to get through), and later the
    //mains come back. Powered things (PoweredLock, PoweredLight) listen to Changed.
    public class PowerGrid : MonoBehaviour
    {
        public enum State { Mains, Blackout, Backup }

        public static PowerGrid Instance { get; private set; }

        [Tooltip("Seconds of total blackout before the backup generators kick in: the window to get through the maglocks.")]
        [SerializeField] float backupDelay = 15f;
        [Tooltip("Seconds on backup before the mains come back (0 = stays on backup).")]
        [SerializeField] float mainsRestoreDelay = 120f;
        [Tooltip("Guards within this distance of the breaker hear it trip and go to look.")]
        [SerializeField] float noiseRadius = 22f;
        [Tooltip("Seconds before nearby guards come to look (they notice the lights go out first): time to slip away.")]
        [SerializeField] float investigateDelay = 3f;
        [SerializeField] bool showCountdown = true;

        public State Current { get; private set; } = State.Mains;
        public bool HasPower => Current != State.Blackout;
        //Seconds until the next change (generators, then mains)
        public float TimeLeft => Mathf.Max(0f, changeAt - Time.time);
        public event Action<State> Changed;

        float changeAt;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        //The breaker at 'at' has been pulled
        public bool CutPower(Vector3 at)
        {
            if (Current != State.Mains) return false;
            Set(State.Blackout, backupDelay);
            StartCoroutine(Investigate(at));
            return true;
        }

        System.Collections.IEnumerator Investigate(Vector3 at)
        {
            yield return new WaitForSeconds(investigateDelay);
            foreach (var guard in FindObjectsOfType<EnemyAI>())
            {
                if (guard.IsDead) continue;
                if (Vector3.Distance(guard.transform.position, at) <= noiseRadius) guard.HearOf(at);
            }
        }

        void Update()
        {
            if (Current == State.Blackout && Time.time >= changeAt) Set(State.Backup, mainsRestoreDelay);
            else if (Current == State.Backup && mainsRestoreDelay > 0f && Time.time >= changeAt) Set(State.Mains, 0f);
        }

        void Set(State s, float duration)
        {
            Current = s;
            changeAt = Time.time + duration;
            Debug.Log("[Power] " + s + (duration > 0f ? " for " + duration + "s" : ""));
            Changed?.Invoke(s);
        }

        GUIStyle style;
        void OnGUI()
        {
            if (!showCountdown || Current != State.Blackout) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.045f), alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = TimeLeft < 5f ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(0, Screen.height * 0.08f, Screen.width, Screen.height * 0.1f), "POWER OUT - BACKUP GENERATORS IN " + TimeLeft.ToString("0.0") + "s", style);
        }
    }
}
