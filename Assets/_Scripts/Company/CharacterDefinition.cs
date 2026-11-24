using UnityEngine;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Ownables;
using Rambler.Stats;

namespace Rambler.Crew
{
    //A character the player can hire. Named characters (unique) are offered as themselves; non-unique ones are
    //bodies a RecruitPool template rolls generated recruits on. The prefab comes from Tools > Character Setup.
    //Keep these in a Resources folder so saves can find them.
    [CreateAssetMenu(fileName = "Character", menuName = "Rambler/Crew/Character")]
    public class CharacterDefinition : OwnableDefinition
    {
        [Tooltip("The playable character prefab (Squad preset from Tools > Character Setup).")]
        [SerializeField] GameObject prefab = null;
        [SerializeField] CharacterType type = CharacterType.Human;
        [SerializeField] CharacterClass characterClass = CharacterClass.HumanPlayer;
        [Tooltip("A named character: only one of them exists. Off = a body for generated recruits.")]
        [SerializeField] bool unique = true;
        [Min(0)] [SerializeField] int wagePerMission = 50;
        [Min(0)] [SerializeField] float startingExperience = 0f;
        [SerializeField] SkillLevel[] startingSkills = new SkillLevel[0];
        [Tooltip("The first is equipped; the rest go in the inventory.")]
        [SerializeField] Weapon[] startingWeapons = new Weapon[0];

        public GameObject Prefab => prefab;
        public CharacterType Type => type;
        public CharacterClass CharacterClass => characterClass;
        public bool Unique => unique;
        public int WagePerMission => wagePerMission;
        public float StartingExperience => startingExperience;
        public SkillLevel[] StartingSkills => startingSkills;
        public Weapon[] StartingWeapons => startingWeapons;
        public Sprite Portrait => Icon;
    }
}
