using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Rambler.Ownables;

namespace Rambler.Crew
{
    public enum CrewStatus
    {
        Candidate,  //For hire
        Hired,      //In the crew, ready to deploy
        OnMission,  //Deployed in the current scene
        Training,   //Sitting out missions on a course
        Injured,    //Sitting out missions recovering (Injuries death rule)
        Dead,       //Permadeath death rule
        Fired,      //Left on good terms; may be offered for hire again
        Hostile     //Left with a grudge; may turn up as an enemy
    }

    //Chosen at game start: what happens to crew who fall in a mission
    public enum DeathRule { Permadeath, Injuries }

    [Serializable]
    public struct SkillRecord
    {
        public string skillId;
        public int level;
    }

    //Practice towards a skill's next level, earned by doing (manning a ship station)
    [Serializable]
    public struct SkillPractice
    {
        public string skillId;
        public float seconds;
    }

    //One person's saved state. The definition is the authored asset (prefab, portrait, type); everything that
    //changes in play lives here, so this, not the ScriptableObject, is what the save file stores.
    [Serializable]
    public class CharacterRecord
    {
        public string recordId;
        public string definitionId;
        public string displayName;
        public bool generated;
        public CrewStatus status;
        public int hireFee;
        public int wage;
        public float experience;
        public int missionsServed;
        public int missionsOut;          //Missions left injured or training
        public string trainingCourseId;
        public List<SkillRecord> skills = new List<SkillRecord>();
        //Before attachments existed records held plain weapon IDs; Weapons converts them on first use
        public List<string> weaponIds = new List<string>();
        public List<WeaponItem> weapons;
        //Fitted gear (one per gear slot: armour, implant)
        public List<string> gearIds = new List<string>();
        //Practice towards each skill's next level (learning by doing)
        [OptionalField] public List<SkillPractice> practice;

        //The guns carried, the first is equipped
        public List<WeaponItem> Weapons
        {
            get
            {
                if (weapons == null)
                {
                    weapons = new List<WeaponItem>();
                    if (weaponIds != null) foreach (var id in weaponIds) weapons.Add(new WeaponItem(id));
                    weaponIds?.Clear();
                }
                return weapons;
            }
        }

        public WeaponItem Equipped => Weapons.Count > 0 ? Weapons[0] : null;

        public UpgradeDefinition GearIn(UpgradeSlot slot)
        {
            if (gearIds == null) gearIds = new List<string>();
            foreach (var id in gearIds)
            {
                var u = CatalogAsset.GetFromID<UpgradeDefinition>(id);
                if (u != null && u.Slot == slot) return u;
            }
            return null;
        }

        //Gear and skill effects (no weapon); add a weapon's attachments for the full picture
        public Rambler.Combat.ModifierSet PersonalModifiers
        {
            get
            {
                var total = new Rambler.Combat.ModifierSet();
                if (gearIds != null)
                    foreach (var id in gearIds)
                    {
                        var u = CatalogAsset.GetFromID<UpgradeDefinition>(id);
                        if (u != null) total += u.Modifiers;
                    }
                foreach (var s in skills)
                {
                    var def = CatalogAsset.GetFromID<SkillDefinition>(s.skillId);
                    if (def != null) total += def.CombatPerLevel * s.level;
                }
                return total;
            }
        }

        public Rambler.Combat.ModifierSet ModifiersWith(WeaponItem weapon) => weapon != null ? PersonalModifiers + weapon.Modifiers : PersonalModifiers;

        public CharacterDefinition Definition => CatalogAsset.GetFromID<CharacterDefinition>(definitionId);
        public TrainingCourse Training => CatalogAsset.GetFromID<TrainingCourse>(trainingCourseId);
        public bool InCrew => status == CrewStatus.Hired || status == CrewStatus.OnMission || status == CrewStatus.Training || status == CrewStatus.Injured;
        public bool CanDeploy => status == CrewStatus.Hired;

        public int GetSkill(SkillDefinition skill)
        {
            if (skill == null) return 0;
            foreach (var s in skills) if (s.skillId == skill.ItemID) return s.level;
            return 0;
        }

        public void SetSkill(SkillDefinition skill, int level)
        {
            if (skill == null) return;
            level = Math.Max(0, Math.Min(level, skill.MaxLevel));
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].skillId != skill.ItemID) continue;
                skills[i] = new SkillRecord { skillId = skill.ItemID, level = level };
                return;
            }
            skills.Add(new SkillRecord { skillId = skill.ItemID, level = level });
        }

        public CrewTrait[] Traits => Definition != null ? Definition.Traits : new CrewTrait[0];
        public bool HasTrait(CrewTrait t) => Array.IndexOf(Traits, t) >= 0;

        //Seconds of practice a skill needs to go from this level to the next
        public static float PracticeNeeded(int level) => 40f * (level + 1);

        public float GetPractice(SkillDefinition skill)
        {
            if (skill == null || practice == null) return 0f;
            foreach (var p in practice) if (p.skillId == skill.ItemID) return p.seconds;
            return 0f;
        }

        //Adds practice; returns true when it raises the skill a level
        public bool Practise(SkillDefinition skill, float seconds)
        {
            if (skill == null || seconds <= 0f) return false;
            int level = GetSkill(skill);
            if (level >= skill.MaxLevel) return false;
            if (practice == null) practice = new List<SkillPractice>();
            int index = practice.FindIndex(p => p.skillId == skill.ItemID);
            float total = (index >= 0 ? practice[index].seconds : 0f) + seconds;
            bool levelled = false;
            if (total >= PracticeNeeded(level)) { total -= PracticeNeeded(level); SetSkill(skill, level + 1); levelled = true; }
            var entry = new SkillPractice { skillId = skill.ItemID, seconds = total };
            if (index >= 0) practice[index] = entry; else practice.Add(entry);
            return levelled;
        }

        //Gives a named character any starting skill they don't have at all (skills authored after their record was
        //made); never lowers or changes one they already have
        public void AddMissingStartingSkills()
        {
            var def = Definition;
            if (def == null || generated) return;
            foreach (var s in def.StartingSkills)
                if (s.skill != null && GetSkill(s.skill) == 0 && !skills.Exists(k => k.skillId == s.skill.ItemID)) SetSkill(s.skill, s.level);
        }

        //A fresh record for a named character, as they first appear for hire
        public static CharacterRecord FromDefinition(CharacterDefinition definition)
        {
            var r = new CharacterRecord
            {
                recordId = Guid.NewGuid().ToString(),
                definitionId = definition.ItemID,
                displayName = definition.DisplayName,
                status = CrewStatus.Candidate,
                hireFee = definition.Price,
                wage = definition.WagePerMission,
                experience = definition.StartingExperience,
            };
            foreach (var s in definition.StartingSkills) r.SetSkill(s.skill, s.level);
            foreach (var w in definition.StartingWeapons) if (w != null) r.Weapons.Add(new WeaponItem(w.GetItemID()));
            return r;
        }
    }
}
