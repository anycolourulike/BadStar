using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Rambler.Combat;

namespace Rambler.Control
{
    //Off-duty idles for guards. A guard that has settled at its post, unalerted, holsters its pistol and:
    //  - pairs up with another settled guard close by: one talks, the other listens, swapping now and then,
    //    and they turn to face each other
    //  - or, with nobody to talk to, lights a cigarette.
    //Anything that wakes the EnemyAI (a sighting, a noise, being shot, moving off) ends it at once: the
    //cigarette goes, the pistol comes back out and the partner is let go.
    //Drives the animator int "Social" (0 none, 1 talk, 2 listen, 3 smoke); the states live in MeatBot Law.
    [RequireComponent(typeof(EnemyAI))]
    public class GuardSocial : MonoBehaviour
    {
        public enum Activity { None = 0, Talk = 1, Listen = 2, Smoke = 3 }

        [Tooltip("Guards closer than this pair up for a chat.")]
        [SerializeField] float pairRadius = 4f;
        [Tooltip("Seconds standing calm at its post before it starts.")]
        [SerializeField] float settleTime = 1.5f;
        [Tooltip("Seconds alone before it lights up instead.")]
        [SerializeField] float soloSmokeDelay = 4f;
        [Tooltip("Seconds before the talker and listener swap (random in this range).")]
        [SerializeField] Vector2 turnLength = new Vector2(6f, 11f);
        [SerializeField] float faceTurnSpeed = 180f;

        [Header("Cigarette")]
        [SerializeField] GameObject cigarettePrefab;
        [SerializeField] HumanBodyBones cigaretteHand = HumanBodyBones.RightHand;
        [SerializeField] Vector3 cigaretteOffset = new Vector3(0.02f, 0.06f, 0.03f);
        [SerializeField] Vector3 cigaretteEuler = new Vector3(0f, 0f, 90f);

        static readonly List<GuardSocial> all = new List<GuardSocial>();
        static readonly int SocialHash = Animator.StringToHash("Social");

        EnemyAI ai;
        Animator anim;
        Fighter fighter;
        bool hasParam;
        float calmSince = -1f;
        float nextSwap;
        GuardSocial partner;
        GameObject cigarette;
        Rig[] rigs;
        float[] rigWeights;
        bool holstered;

        public Activity Current { get; private set; }
        public GuardSocial Partner => partner;

        //Settled at its post and nothing on its mind
        //Patrolling guards count too while stopped at a waypoint (a long stop is a break)
        bool Calm => ai != null && ai.isActiveAndEnabled && !ai.IsDead && (ai.StateName == "Idle" || ai.StateName == "Patrol") && !ai.TargetVisible &&
                     !ai.HasLead && ai.TimeSinceSeen > 5f && ai.Agent != null && ai.Agent.velocity.sqrMagnitude < 0.01f;
        //A patrol stop has to last a while before it's a break (short waypoint pauses don't holster the gun)
        float SettleNeeded => ai != null && ai.StateName == "Patrol" ? Mathf.Max(settleTime, 3.5f) : settleTime;
        bool Ready => Calm && calmSince >= 0f && Time.time - calmSince >= SettleNeeded;

        void Awake()
        {
            ai = GetComponent<EnemyAI>();
            anim = GetComponent<Animator>();
            fighter = GetComponent<Fighter>();
            if (anim != null) foreach (var p in anim.parameters) if (p.nameHash == SocialHash) hasParam = true;
            rigs = GetComponentsInChildren<Rig>(true);
            rigWeights = new float[rigs.Length];
        }

        void OnEnable() => all.Add(this);

        void OnDisable()
        {
            all.Remove(this);
            Stop();
        }

        void Update()
        {
            if (!Calm)
            {
                calmSince = -1f;
                if (Current != Activity.None) Stop();
                return;
            }
            if (calmSince < 0f) calmSince = Time.time;
            if (Time.time - calmSince < SettleNeeded) return;

            //The partner walked off or was woken up
            if (partner != null && (partner.partner != this || !partner.Ready)) Unpair();

            if (partner == null)
            {
                var other = FindPartner();
                if (other != null) Pair(other);
                else if (Current == Activity.None && Time.time - calmSince >= soloSmokeDelay) Set(Activity.Smoke);
            }
            else if (Current == Activity.Talk && Time.time >= nextSwap)
            {
                //Their turn to talk
                Set(Activity.Listen);
                partner.Set(Activity.Talk);
                nextSwap = partner.nextSwap = Time.time + Random.Range(turnLength.x, turnLength.y);
            }
        }

        void LateUpdate()
        {
            //Face each other (after EnemyIdle has turned it towards its post)
            if (partner == null) return;
            Vector3 to = partner.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), faceTurnSpeed * Time.deltaTime);
        }

        GuardSocial FindPartner()
        {
            GuardSocial best = null;
            float bestDist = pairRadius;
            foreach (var g in all)
            {
                if (g == this || g.partner != null || !g.Ready) continue;
                float d = Vector3.Distance(g.transform.position, transform.position);
                if (d < bestDist) { best = g; bestDist = d; }
            }
            return best;
        }

        void Pair(GuardSocial other)
        {
            partner = other;
            other.partner = this;
            bool iTalk = Random.value < 0.5f;
            Set(iTalk ? Activity.Talk : Activity.Listen);
            other.Set(iTalk ? Activity.Listen : Activity.Talk);
            nextSwap = other.nextSwap = Time.time + Random.Range(turnLength.x, turnLength.y);
        }

        void Unpair()
        {
            if (partner != null && partner.partner == this) partner.partner = null;
            partner = null;
            Set(Activity.None); //Back to standing; it may light up or find someone else
            calmSince = Time.time;
        }

        //Woken up: drop everything and let the partner go
        public void Stop()
        {
            if (partner != null)
            {
                var p = partner;
                partner = null;
                if (p.partner == this) { p.partner = null; p.Set(Activity.None); p.calmSince = Time.time; }
            }
            Set(Activity.None);
        }

        void Set(Activity a)
        {
            if (a == Current) return;
            if (a != Activity.None) Holster(); else Draw();
            Current = a;
            if (hasParam) anim.SetInteger(SocialHash, (int)a);
            ShowCigarette(a == Activity.Smoke);
        }

        //Pistol away and arms free of the weapon rig
        void Holster()
        {
            if (holstered) return;
            holstered = true;
            if (fighter != null)
            {
                if (fighter.activeWeapon != null) fighter.activeWeapon.gameObject.SetActive(false);
                fighter.RigWeightToZero();
            }
            for (int i = 0; i < rigs.Length; i++) { rigWeights[i] = rigs[i].weight; rigs[i].weight = 0f; }
        }

        void Draw()
        {
            if (!holstered) return;
            holstered = false;
            for (int i = 0; i < rigs.Length; i++) rigs[i].weight = rigWeights[i];
            if (fighter != null)
            {
                if (fighter.activeWeapon != null) fighter.activeWeapon.gameObject.SetActive(true);
                if (fighter.weaponConfig != null && !fighter.weaponConfig.isMelee) fighter.RigWeightToOne();
            }
        }

        void ShowCigarette(bool on)
        {
            if (on && cigarette == null && cigarettePrefab != null && anim != null && anim.isHuman)
            {
                var hand = anim.GetBoneTransform(cigaretteHand);
                if (hand != null)
                {
                    cigarette = Instantiate(cigarettePrefab, hand);
                    cigarette.transform.localPosition = cigaretteOffset;
                    cigarette.transform.localRotation = Quaternion.Euler(cigaretteEuler);
                    //Keep its real size whatever the bone's scale
                    Vector3 ls = hand.lossyScale;
                    cigarette.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, ls.x), 1f / Mathf.Max(0.001f, ls.y), 1f / Mathf.Max(0.001f, ls.z));
                }
            }
            if (cigarette != null) cigarette.SetActive(on);
        }
    }
}
