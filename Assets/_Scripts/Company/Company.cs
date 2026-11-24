using System;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Ownables;
using Rambler.Saving;

namespace Rambler.Crew
{
    //The player's outfit: money, crew, the current list of recruits, and everyone who has left or fallen.
    //One copy lives for the whole game (it makes itself on the first scene load) and is saved with the game
    //under a fixed key, not per scene. Ships and businesses will be added here.
    public class Company : MonoBehaviour, ISaveable
    {
        const string SaveKey = "Company";
        const string PoolResource = "Company/Recruit Pool";

        public static Company Instance { get; private set; }

        [SerializeField] int credits = 1000;
        [Tooltip("Set at game start: Permadeath, or fallen crew sit out missions injured.")]
        [SerializeField] DeathRule deathRule = DeathRule.Permadeath;
        [SerializeField] RecruitPool recruitPool = null;
        [Min(1)] [SerializeField] int maxCrew = 12;
        [Tooltip("Chance a fired crew member leaves with a grudge and may turn up as an enemy.")]
        [Range(0f, 1f)] [SerializeField] float grudgeChance = 0.35f;
        [Tooltip("Chance each fired (no grudge) former crew member is back on the recruit list.")]
        [Range(0f, 1f)] [SerializeField] float rehireChance = 0.25f;
        [Tooltip("Former crew ask for this much more to come back.")]
        [SerializeField] float rehireFeeMultiplier = 1.5f;
        [Tooltip("Missions an injured crew member sits out (Injuries death rule).")]
        [Min(1)] [SerializeField] int injuryMissions = 2;

        readonly List<CharacterRecord> crew = new List<CharacterRecord>();
        readonly List<CharacterRecord> candidates = new List<CharacterRecord>();
        readonly List<CharacterRecord> former = new List<CharacterRecord>();
        readonly List<CharacterRecord> fallen = new List<CharacterRecord>();

        //Raised whenever money, crew or the recruit list change (the UI screens listen to this)
        public event Action Changed;

        public int Credits => credits;
        public DeathRule DeathRule => deathRule;
        public int MaxCrew => maxCrew;
        public RecruitPool RecruitPool => recruitPool;
        public IReadOnlyList<CharacterRecord> Crew => crew;
        public IReadOnlyList<CharacterRecord> Candidates => candidates;
        public IReadOnlyList<CharacterRecord> Former => former;
        public IReadOnlyList<CharacterRecord> Fallen => fallen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("Company").AddComponent<Company>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (recruitPool == null) recruitPool = Resources.Load<RecruitPool>(PoolResource);
            SavingSystem.RegisterGlobal(SaveKey, this);
        }

        void Start()
        {
            if (candidates.Count == 0) RefreshCandidates();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SavingSystem.UnregisterGlobal(SaveKey, this);
            Instance = null;
        }

        // ---------- Money ----------

        public bool CanAfford(int amount) => credits >= amount;

        public void AddCredits(int amount)
        {
            credits += amount;
            Changed?.Invoke();
        }

        public bool Spend(int amount)
        {
            if (amount < 0 || credits < amount) return false;
            credits -= amount;
            Changed?.Invoke();
            return true;
        }

        //Picked once at the start of a new game
        public void SetDeathRule(DeathRule rule)
        {
            deathRule = rule;
            Changed?.Invoke();
        }

        // ---------- Hiring and firing ----------

        public bool CanHire(CharacterRecord r, out string reason)
        {
            reason = null;
            if (r == null || !candidates.Contains(r)) reason = "Not for hire";
            else if (CrewCount() >= maxCrew) reason = "Crew is full (" + maxCrew + ")";
            else if (!CanAfford(r.hireFee)) reason = "Needs " + r.hireFee + " credits";
            return reason == null;
        }

        public bool Hire(CharacterRecord r)
        {
            if (!CanHire(r, out string reason)) { Debug.Log("[Company] Can't hire " + r?.displayName + ": " + reason); return false; }
            credits -= r.hireFee;
            candidates.Remove(r);
            former.Remove(r);
            r.status = CrewStatus.Hired;
            crew.Add(r);
            Changed?.Invoke();
            return true;
        }

        //They leave with their gear. Some hold a grudge and may turn up later as enemies.
        public void Fire(CharacterRecord r)
        {
            if (r == null || !crew.Remove(r)) return;
            r.status = UnityEngine.Random.value < grudgeChance ? CrewStatus.Hostile : CrewStatus.Fired;
            r.trainingCourseId = null;
            r.missionsOut = 0;
            former.Add(r);
            Changed?.Invoke();
        }

        //Former crew holding a grudge, for enemy spawners to dress up as hostiles
        public IEnumerable<CharacterRecord> HostileFormerCrew()
        {
            foreach (var r in former) if (r.status == CrewStatus.Hostile) yield return r;
        }

        //A grudge settled (killed or bought off): they never come back
        public void SettleGrudge(CharacterRecord r, bool killed)
        {
            if (r == null || !former.Remove(r)) return;
            if (killed) { r.status = CrewStatus.Dead; fallen.Add(r); }
            Changed?.Invoke();
        }

        public void RefreshCandidates()
        {
            candidates.Clear();
            if (recruitPool != null)
                candidates.AddRange(recruitPool.MakeCandidates(recruitPool.ListSize, NamedAvailable));
            foreach (var r in former)
            {
                if (r.status != CrewStatus.Fired || UnityEngine.Random.value >= rehireChance) continue;
                r.status = CrewStatus.Candidate;
                r.hireFee = Mathf.RoundToInt(r.hireFee * rehireFeeMultiplier);
                candidates.Add(r);
            }
            Changed?.Invoke();
        }

        //A named character can be offered only if nobody already holds that definition
        bool NamedAvailable(CharacterDefinition d)
        {
            foreach (var list in new[] { crew, former, fallen, candidates })
                foreach (var r in list)
                    if (!r.generated && r.definitionId == d.ItemID) return false;
            return true;
        }

        int CrewCount() => crew.Count;

        public CharacterRecord GetRecord(string recordId)
        {
            foreach (var list in new[] { crew, candidates, former, fallen })
                foreach (var r in list)
                    if (r.recordId == recordId) return r;
            return null;
        }

        // ---------- Missions ----------

        public void MarkDeployed(CharacterRecord r)
        {
            if (r == null || !crew.Contains(r)) return;
            r.status = CrewStatus.OnMission;
            Changed?.Invoke();
        }

        //A crew member fell: dead for good, or out injured, by the death rule
        public void ReportDown(CharacterRecord r)
        {
            if (r == null || !crew.Contains(r)) return;
            if (deathRule == DeathRule.Permadeath)
            {
                crew.Remove(r);
                r.status = CrewStatus.Dead;
                fallen.Add(r);
            }
            else
            {
                r.status = CrewStatus.Injured;
                r.missionsOut = injuryMissions;
            }
            Changed?.Invoke();
        }

        //End of a mission: the payout comes in, wages go out (credits can go into debt), the deployed are back,
        //injuries and training tick down, and a new list of recruits turns up.
        public void CompleteMission(int payout)
        {
            credits += payout;
            foreach (var r in crew)
            {
                credits -= r.wage;
                switch (r.status)
                {
                    case CrewStatus.OnMission:
                        r.missionsServed++;
                        r.status = CrewStatus.Hired;
                        break;
                    case CrewStatus.Injured:
                        if (--r.missionsOut <= 0) r.status = CrewStatus.Hired;
                        break;
                    case CrewStatus.Training:
                        if (--r.missionsOut <= 0) FinishTraining(r);
                        break;
                }
            }
            RefreshCandidates();
        }

        // ---------- Training ----------

        public bool CanTrain(CharacterRecord r, TrainingCourse course, out string reason)
        {
            reason = null;
            if (r == null || course == null || course.Skill == null) reason = "No course";
            else if (!crew.Contains(r) || r.status != CrewStatus.Hired) reason = r.displayName + " isn't free";
            else if (r.GetSkill(course.Skill) > course.MaxStartLevel) reason = "Too skilled for this course";
            else if (r.GetSkill(course.Skill) >= course.Skill.MaxLevel) reason = course.Skill.DisplayName + " is maxed";
            else if (!CanAfford(course.Price)) reason = "Needs " + course.Price + " credits";
            return reason == null;
        }

        public bool StartTraining(CharacterRecord r, TrainingCourse course)
        {
            if (!CanTrain(r, course, out _)) return false;
            credits -= course.Price;
            r.status = CrewStatus.Training;
            r.trainingCourseId = course.ItemID;
            r.missionsOut = course.Missions;
            Changed?.Invoke();
            return true;
        }

        void FinishTraining(CharacterRecord r)
        {
            var course = r.Training;
            if (course != null) r.SetSkill(course.Skill, r.GetSkill(course.Skill) + course.LevelsGained);
            r.trainingCourseId = null;
            r.missionsOut = 0;
            r.status = CrewStatus.Hired;
        }

        // ---------- Saving ----------

        [Serializable]
        class CompanyState
        {
            public int credits;
            public DeathRule deathRule;
            public List<CharacterRecord> crew, candidates, former, fallen;
        }

        object ISaveable.CaptureState()
        {
            return new CompanyState
            {
                credits = credits,
                deathRule = deathRule,
                crew = new List<CharacterRecord>(crew),
                candidates = new List<CharacterRecord>(candidates),
                former = new List<CharacterRecord>(former),
                fallen = new List<CharacterRecord>(fallen),
            };
        }

        void ISaveable.RestoreState(object state)
        {
            if (!(state is CompanyState s)) return;
            credits = s.credits;
            deathRule = s.deathRule;
            Fill(crew, s.crew); Fill(candidates, s.candidates); Fill(former, s.former); Fill(fallen, s.fallen);
            //Saved mid-mission: whoever was deployed is back in the crew
            foreach (var r in crew) if (r.status == CrewStatus.OnMission) r.status = CrewStatus.Hired;
            Changed?.Invoke();
        }

        static void Fill(List<CharacterRecord> target, List<CharacterRecord> source)
        {
            target.Clear();
            if (source != null) target.AddRange(source);
        }
    }
}
