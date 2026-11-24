using System.Collections.Generic;
using UnityEngine;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Inventories;
using Rambler.Movement;
using Rambler.Ownables;
using Rambler.Stats;

namespace Rambler.Crew
{
    //Joins a spawned character to their saved CharacterRecord: applies the record's name, experience, skills,
    //weapons, attachments and gear when they spawn, reports a fall to the Company, and writes experience and
    //weapons back when they leave the scene. Added by SquadDeployer; Bind it before the character is first activated.
    //Combat effects go through CombatModifiers (Fighter, ActiveWeapon and Health read it): gear and skills always,
    //plus the attachments on whichever gun is in hand.
    [DisallowMultipleComponent]
    public class RosterMember : MonoBehaviour, IModifierProvider
    {
        [SerializeField] string recordId;

        bool down;
        Fighter fighter;
        CombatModifiers modifiers;
        Weapon lastWeapon;

        //Looked up each time: loading a save replaces the Company's record objects
        public CharacterRecord Record => Company.Instance != null ? Company.Instance.GetRecord(recordId) : null;
        public string RecordId => recordId;

        //Call while the character is still inactive, so Fighter and BaseStats start from the record
        public void Bind(CharacterRecord record)
        {
            recordId = record.recordId;
            gameObject.name = record.displayName;

            var experience = GetComponent<Experience>();
            if (experience != null) experience.RestoreState(record.experience);

            var stats = GetComponent<BaseStats>();
            if (stats != null) stats.EnableModifiers();

            fighter = GetComponent<Fighter>();
            var first = record.Equipped != null ? record.Equipped.Weapon : null;
            if (fighter != null && first != null) fighter.weaponConfig = first;

            //Before the gun spawns, so its magazine size already counts the attachments
            modifiers = GetComponent<CombatModifiers>();
            if (modifiers == null) modifiers = gameObject.AddComponent<CombatModifiers>();
            ApplyModifiers(record, first);

            //Gear can make them quicker or slower (Mover feeds the animation from the agent's speed, so feet keep up)
            var mover = GetComponent<Mover>();
            float speed = modifiers.Speed;
            if (mover != null && !Mathf.Approximately(speed, 1f)) mover.maxSpeed *= speed;
        }

        void ApplyModifiers(CharacterRecord record, Weapon inHand)
        {
            if (record == null || modifiers == null) return;
            WeaponItem item = null;
            foreach (var w in record.Weapons) if (inHand != null && w.weaponId == inHand.GetItemID()) { item = w; break; }
            modifiers.Set(record.ModifiersWith(item));
            lastWeapon = inHand;
        }

        void OnEnable()
        {
            Health.playerDeath += OnFell;
            Health.companionDeath += OnFell;
        }

        void OnDisable()
        {
            Health.playerDeath -= OnFell;
            Health.companionDeath -= OnFell;
        }

        //Fighter.Start files the equipped weapon in the inventory; the rest of the record's weapons go in here
        void Start()
        {
            var record = Record;
            var inventory = GetComponent<Inventory>();
            if (record == null || inventory == null) return;
            for (int i = 1; i < record.Weapons.Count; i++)
            {
                var w = record.Weapons[i].Weapon;
                if (w != null && !inventory.HasItem(w)) inventory.AddToFirstEmptySlot(w, 1);
            }
        }

        //Switching guns mid-mission swaps which attachments count
        void Update()
        {
            if (fighter != null && fighter.weaponConfig != lastWeapon) ApplyModifiers(Record, fighter.weaponConfig);
        }

        void OnFell(GameObject character)
        {
            if (character != gameObject || down) return;
            down = true;
            WriteBack();
            if (Company.Instance != null) Company.Instance.ReportDown(Record);
        }

        void OnDestroy()
        {
            if (!down) WriteBack();
        }

        //Experience earned and the guns carried go back on the record. A gun they still have keeps its
        //attachments; one picked up in the field arrives bare; one dropped is gone, with whatever was on it.
        void WriteBack()
        {
            var record = Record;
            if (record == null) return;

            var experience = GetComponent<Experience>();
            if (experience != null) record.experience = experience.GetPoints();

            var inventory = GetComponent<Inventory>();
            if (fighter == null) fighter = GetComponent<Fighter>();
            if (inventory == null) return;
            var before = new List<WeaponItem>(record.Weapons);
            var after = new List<WeaponItem>();
            void Keep(Weapon w)
            {
                if (w == null || string.IsNullOrEmpty(w.GetItemID()) || w.name.ToLowerInvariant() == "unarmed") return;
                string id = w.GetItemID();
                foreach (var a in after) if (a.weaponId == id) return;
                var existing = before.Find(b => b.weaponId == id);
                if (existing != null) before.Remove(existing);
                after.Add(existing ?? new WeaponItem(id));
            }
            if (fighter != null) Keep(fighter.weaponConfig); //Equipped first, so it's equipped next time
            try
            {
                for (int i = 0; i < inventory.GetSize(); i++) Keep(inventory.GetItemInSlot(i));
            }
            catch (System.NullReferenceException) { return; } //Inventory never woke (destroyed before activation)
            record.Weapons.Clear();
            record.Weapons.AddRange(after);
        }

        // ---------- Skill and gear bonuses to stats (BaseStats asks every IModifierProvider) ----------

        public IEnumerable<float> GetAdditiveModifiers(Stat stat)
        {
            var record = Record;
            if (record == null) yield break;
            foreach (var s in record.skills)
            {
                var skill = CatalogAsset.GetFromID<SkillDefinition>(s.skillId);
                if (skill == null) continue;
                foreach (var b in skill.Bonuses) if (b.stat == stat && b.additivePerLevel != 0f) yield return b.additivePerLevel * s.level;
            }
        }

        public IEnumerable<float> GetPercentageModifiers(Stat stat)
        {
            var record = Record;
            if (record == null) yield break;
            foreach (var s in record.skills)
            {
                var skill = CatalogAsset.GetFromID<SkillDefinition>(s.skillId);
                if (skill == null) continue;
                foreach (var b in skill.Bonuses) if (b.stat == stat && b.percentPerLevel != 0f) yield return b.percentPerLevel * s.level;
            }
            //Implants and armour that add health
            if (stat == Stat.Health)
            {
                float gearHealth = record.PersonalModifiers.health;
                if (gearHealth != 0f) yield return gearHealth;
            }
        }
    }
}
