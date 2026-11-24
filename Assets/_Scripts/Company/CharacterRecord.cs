using System;
using System.Collections.Generic;
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
