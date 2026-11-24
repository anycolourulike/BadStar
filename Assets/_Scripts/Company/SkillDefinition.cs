using System;
using UnityEngine;
using Rambler.Ownables;
using Rambler.Stats;

namespace Rambler.Crew
{
    //A skill crew can have and train (Firearms, Hacking, ...). Each level adds its stat bonuses to the character.
    //Skills with no bonuses are still recorded; gameplay reads their level directly (e.g. hacking speed).
    [CreateAssetMenu(fileName = "Skill", menuName = "Rambler/Crew/Skill")]
    public class SkillDefinition : CatalogAsset
    {
        [Serializable]
        public struct StatBonus
        {
            public Stat stat;
            [Tooltip("Added to the stat per skill level.")]
            public float additivePerLevel;
            [Tooltip("Percent added to the stat per skill level.")]
            public float percentPerLevel;
        }

        [Min(1)] [SerializeField] int maxLevel = 10;
        [SerializeField] StatBonus[] bonuses = new StatBonus[0];
        [Tooltip("Combat changes per skill level (e.g. Firearms: spread -4% a level).")]
        [SerializeField] Rambler.Combat.ModifierSet combatPerLevel;

        public int MaxLevel => maxLevel;
        public StatBonus[] Bonuses => bonuses;
        public Rambler.Combat.ModifierSet CombatPerLevel => combatPerLevel;
    }

    [Serializable]
    public struct SkillLevel
    {
        public SkillDefinition skill;
        [Min(0)] public int level;
    }
}
