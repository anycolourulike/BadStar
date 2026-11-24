using System.Collections.Generic;
using UnityEngine;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Inventories;
using Rambler.Ownables;
using Rambler.Stats;

namespace Rambler.Crew
{
    //Joins a spawned character to their saved CharacterRecord: applies the record's name, experience, skills
    //and weapons when they spawn, reports a fall to the Company, and writes experience and gear back when
    //they leave the scene. Added by SquadDeployer; Bind it before the character is first activated.
    [DisallowMultipleComponent]
    public class RosterMember : MonoBehaviour, IModifierProvider
    {
        [SerializeField] string recordId;

        bool down;

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

            var fighter = GetComponent<Fighter>();
            var first = record.weaponIds.Count > 0 ? Weapon.GetFromID(record.weaponIds[0]) : null;
            if (fighter != null && first != null) fighter.weaponConfig = first;
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
            for (int i = 1; i < record.weaponIds.Count; i++)
            {
                var w = Weapon.GetFromID(record.weaponIds[i]);
                if (w != null && !inventory.HasItem(w)) inventory.AddToFirstEmptySlot(w, 1);
            }
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

        //Experience earned and the weapons carried go back on the record
        void WriteBack()
        {
            var record = Record;
            if (record == null) return;

            var experience = GetComponent<Experience>();
            if (experience != null) record.experience = experience.GetPoints();

            var inventory = GetComponent<Inventory>();
            var fighter = GetComponent<Fighter>();
            if (inventory == null) return;
            var ids = new List<string>();
            void Keep(Weapon w)
            {
                if (w == null || string.IsNullOrEmpty(w.GetItemID()) || w.name.ToLowerInvariant() == "unarmed") return;
                if (!ids.Contains(w.GetItemID())) ids.Add(w.GetItemID());
            }
            if (fighter != null) Keep(fighter.weaponConfig); //Equipped first, so it's equipped next time
            try
            {
                for (int i = 0; i < inventory.GetSize(); i++) Keep(inventory.GetItemInSlot(i));
            }
            catch (System.NullReferenceException) { return; } //Inventory never woke (destroyed before activation)
            record.weaponIds = ids;
        }

        // ---------- Skill bonuses (BaseStats asks every IModifierProvider) ----------

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
        }
    }
}
