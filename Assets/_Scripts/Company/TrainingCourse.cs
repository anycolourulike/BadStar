using UnityEngine;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //Training bought in the Management screen: raises one skill after the trainee sits out some missions
    [CreateAssetMenu(fileName = "Training Course", menuName = "Rambler/Crew/Training Course")]
    public class TrainingCourse : OwnableDefinition
    {
        [SerializeField] SkillDefinition skill = null;
        [Min(1)] [SerializeField] int levelsGained = 1;
        [Tooltip("Missions the trainee sits out before the course completes.")]
        [Min(1)] [SerializeField] int missions = 1;
        [Tooltip("Highest current skill level the course can be taken at.")]
        [Min(0)] [SerializeField] int maxStartLevel = 9;

        public SkillDefinition Skill => skill;
        public int LevelsGained => levelsGained;
        public int Missions => missions;
        public int MaxStartLevel => maxStartLevel;
    }
}
