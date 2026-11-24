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
    public partial class Company : MonoBehaviour, ISaveable
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
        [Tooltip("Weapons one crew member can carry (the first is equipped).")]
        [Min(1)] [SerializeField] int maxCarried = 3;

        [Tooltip("What the Home Terminal shop sells (Resources/Company/Shop Catalogue when empty).")]
        [SerializeField] ShopCatalogue shop = null;

        readonly List<CharacterRecord> crew = new List<CharacterRecord>();
        //Guns in the Company armoury, each with its attachments
        readonly List<WeaponItem> armoury = new List<WeaponItem>();
        //Upgrades owned but not fitted (bought, looted, rewarded), one entry each
        readonly List<string> stash = new List<string>();
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
        public IReadOnlyList<WeaponItem> Armoury => armoury;
        public IReadOnlyList<string> Stash => stash;
        public int MaxCarried => maxCarried;
        public ShopCatalogue Shop => shop;

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
            if (shop == null) shop = Resources.Load<ShopCatalogue>("Company/Shop Catalogue");
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

        //End of a mission: the payout (and any reward gear and guns) comes in, wages go out (credits can go into
        //debt), the deployed are back, injuries and training tick down, and a new list of recruits turns up.
        public void CompleteMission(int payout, IEnumerable<UpgradeDefinition> rewards = null, IEnumerable<Rambler.Combat.Weapon> weaponRewards = null)
        {
            credits += payout;
            PayBusinesses();
            if (rewards != null) foreach (var u in rewards) if (u != null) stash.Add(u.ItemID);
            if (weaponRewards != null) foreach (var w in weaponRewards) if (w != null) armoury.Add(new WeaponItem(w.GetItemID()));
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

        // ---------- Loadout and armoury ----------

        //Gear can't change while someone is out in the field (their spawned copy owns it until they're back)
        public bool CanEditLoadout(CharacterRecord r, out string reason)
        {
            reason = null;
            if (r == null || !crew.Contains(r)) reason = "Not in the crew";
            else if (r.status == CrewStatus.OnMission) reason = r.displayName + " is on a mission";
            return reason == null;
        }

        //Make one of their carried guns the equipped one
        public bool EquipWeapon(CharacterRecord r, WeaponItem w)
        {
            if (!CanEditLoadout(r, out _)) return false;
            int i = r.Weapons.IndexOf(w);
            if (i <= 0) return i == 0;
            r.Weapons.RemoveAt(i);
            r.Weapons.Insert(0, w);
            Changed?.Invoke();
            return true;
        }

        //Into the armoury, attachments and all
        public bool StoreWeapon(CharacterRecord r, WeaponItem w)
        {
            if (!CanEditLoadout(r, out _) || !r.Weapons.Remove(w)) return false;
            armoury.Add(w);
            Changed?.Invoke();
            return true;
        }

        public bool CanTakeWeapon(CharacterRecord r, WeaponItem w, out string reason)
        {
            if (!CanEditLoadout(r, out reason)) return false;
            if (!armoury.Contains(w)) reason = "Not in the armoury";
            else if (r.Weapons.Count >= maxCarried) reason = "Carries " + maxCarried + " max";
            else foreach (var c in r.Weapons) if (c.weaponId == w.weaponId) reason = "Has one";
            return reason == null;
        }

        public bool TakeWeapon(CharacterRecord r, WeaponItem w)
        {
            if (!CanTakeWeapon(r, w, out _)) return false;
            armoury.Remove(w);
            r.Weapons.Add(w);
            Changed?.Invoke();
            return true;
        }

        //Where an upgrade from the stash would go on this person: gear in its slot; an attachment on the equipped
        //gun if it fits, otherwise the first carried gun it fits
        public bool CanFit(CharacterRecord r, UpgradeDefinition u, out WeaponItem target, out string reason)
        {
            target = null;
            if (!CanEditLoadout(r, out reason)) return false;
            if (u == null || !stash.Contains(u.ItemID)) { reason = "Not in the stash"; return false; }
            if (u.IsGear) return true;
            foreach (var w in r.Weapons)
                if (u.FitsWeapon(w.Weapon)) { target = w; return true; }
            reason = "Fits no gun carried";
            return false;
        }

        //Fits it from the stash; whatever was in that slot goes back to the stash
        public bool Fit(CharacterRecord r, UpgradeDefinition u)
        {
            if (!CanFit(r, u, out var target, out _)) return false;
            stash.Remove(u.ItemID);
            if (u.IsGear)
            {
                var old = r.GearIn(u.Slot);
                if (old != null) { r.gearIds.Remove(old.ItemID); stash.Add(old.ItemID); }
                r.gearIds.Add(u.ItemID);
            }
            else
            {
                var old = target.AttachmentIn(u.Slot);
                if (old != null) { target.attachmentIds.Remove(old.ItemID); stash.Add(old.ItemID); }
                target.attachmentIds.Add(u.ItemID);
            }
            Changed?.Invoke();
            return true;
        }

        public bool RemoveAttachment(CharacterRecord r, WeaponItem w, UpgradeSlot slot)
        {
            if (!CanEditLoadout(r, out _) || w == null) return false;
            var u = w.AttachmentIn(slot);
            if (u == null) return false;
            w.attachmentIds.Remove(u.ItemID);
            stash.Add(u.ItemID);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveGear(CharacterRecord r, UpgradeSlot slot)
        {
            if (!CanEditLoadout(r, out _)) return false;
            var u = r.GearIn(slot);
            if (u == null) return false;
            r.gearIds.Remove(u.ItemID);
            stash.Add(u.ItemID);
            Changed?.Invoke();
            return true;
        }

        // ---------- Shop, loot and rewards ----------

        public bool Buy(UpgradeDefinition u)
        {
            if (u == null || !Spend(u.Price)) return false;
            stash.Add(u.ItemID);
            Changed?.Invoke();
            return true;
        }

        public bool BuyWeapon(Rambler.Combat.Weapon w, int price)
        {
            if (w == null || !Spend(price)) return false;
            armoury.Add(new WeaponItem(w.GetItemID()));
            Changed?.Invoke();
            return true;
        }

        //Loot pickups and mission rewards
        public void AddToStash(UpgradeDefinition u)
        {
            if (u == null) return;
            stash.Add(u.ItemID);
            Changed?.Invoke();
        }

        public void AddToArmoury(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            armoury.Add(new WeaponItem(weaponId));
            Changed?.Invoke();
        }

        public int StashCount(UpgradeDefinition u)
        {
            int n = 0;
            if (u != null) foreach (var id in stash) if (id == u.ItemID) n++;
            return n;
        }

        // ---------- Saving ----------

        [Serializable]
        class CompanyState
        {
            public int credits;
            public DeathRule deathRule;
            public List<CharacterRecord> crew, candidates, former, fallen;
            public List<string> armoury;        //Saves from before attachments: plain weapon IDs
            public List<WeaponItem> armouryItems;
            public List<string> stash;
            [System.Runtime.Serialization.OptionalField] public List<ShipRecord> ships;
            [System.Runtime.Serialization.OptionalField] public List<BusinessRecord> businesses;
            [System.Runtime.Serialization.OptionalField] public List<string> spareParts;
            [System.Runtime.Serialization.OptionalField] public string activeShipId;
        }

        object ISaveable.CaptureState()
        {
            var state = new CompanyState
            {
                credits = credits,
                deathRule = deathRule,
                crew = new List<CharacterRecord>(crew),
                candidates = new List<CharacterRecord>(candidates),
                former = new List<CharacterRecord>(former),
                fallen = new List<CharacterRecord>(fallen),
                armouryItems = new List<WeaponItem>(armoury),
                stash = new List<string>(stash),
            };
            CaptureFleet(state);
            return state;
        }

        void ISaveable.RestoreState(object state)
        {
            if (!(state is CompanyState s)) return;
            credits = s.credits;
            deathRule = s.deathRule;
            Fill(crew, s.crew); Fill(candidates, s.candidates); Fill(former, s.former); Fill(fallen, s.fallen);
            armoury.Clear();
            if (s.armouryItems != null) armoury.AddRange(s.armouryItems);
            else if (s.armoury != null) foreach (var id in s.armoury) armoury.Add(new WeaponItem(id));
            stash.Clear();
            if (s.stash != null) stash.AddRange(s.stash);
            RestoreFleet(s);
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
