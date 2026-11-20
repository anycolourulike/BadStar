using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

namespace Rambler.Mission
{
    //Pervis' talent: walk up to settled guards and keep them talking. Guards he's chatting to only notice what's
    //right next to them. They get bored after a while, so the rest of the plan has to happen inside that time.
    public class Distractor : MonoBehaviour
    {
        [SerializeField] float reach = 3f;
        [Tooltip("Seconds the guards stay hooked before they get bored and look around again.")]
        [SerializeField] float attentionSpan = 50f;
        [SerializeField] float turnSpeed = 120f;

        readonly List<EnemyAI> hooked = new List<EnemyAI>();
        float startedAt = -1f;
        bool bored;

        public bool Active => hooked.Count > 0;
        public float TimeLeft => startedAt < 0f ? attentionSpan : Mathf.Max(0f, attentionSpan - (Time.time - startedAt));

        void Update()
        {
            if (SquadOrders.Alarm || bored) { Release(); return; }
            //Hook any calm guard in reach (a chatting pair is hooked together)
            foreach (var guard in FindObjectsOfType<EnemyAI>())
            {
                if (guard.IsDead || hooked.Contains(guard)) continue;
                if (guard.StateName != "Idle" || guard.TargetVisible || guard.HasLead) continue;
                if (Vector3.Distance(guard.transform.position, transform.position) > reach) continue;
                guard.Distracted = true;
                hooked.Add(guard);
                if (startedAt < 0f) startedAt = Time.time;
            }
            //Anyone woken up by something else is no longer listening
            for (int i = hooked.Count - 1; i >= 0; i--)
            {
                var g = hooked[i];
                if (g == null || g.IsDead || g.StateName != "Idle" || g.TargetVisible || g.HasLead) { if (g != null) g.Distracted = false; hooked.RemoveAt(i); }
            }
            if (startedAt >= 0f && TimeLeft <= 0f) { bored = true; Release(); Debug.Log("[Mission] the guards got bored of Pervis"); }
        }

        void LateUpdate()
        {
            //Guards he's talking to half-turn towards him
            foreach (var g in hooked)
            {
                Vector3 to = transform.position - g.transform.position; to.y = 0f;
                if (to.sqrMagnitude < 0.01f) continue;
                var want = Quaternion.Slerp(g.transform.rotation, Quaternion.LookRotation(to), 0.35f);
                g.transform.rotation = Quaternion.RotateTowards(g.transform.rotation, want, turnSpeed * Time.deltaTime);
            }
        }

        void Release()
        {
            foreach (var g in hooked) if (g != null) g.Distracted = false;
            hooked.Clear();
        }

        void OnDisable() => Release();
    }
}
