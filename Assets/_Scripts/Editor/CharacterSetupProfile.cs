using UnityEngine;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Stats;

namespace Rambler.EditorTools
{
    //Recipe for one CharacterType: which working character to copy the gameplay setup from,
    //and what to change on the copy. Used by the Character Setup tool (Tools > Character Setup).
    [CreateAssetMenu(fileName = "Character Setup Profile", menuName = "Rambler/Character Setup Profile")]
    public class CharacterSetupProfile : ScriptableObject
    {
        public CharacterType type;

        [Header("Copy From")]
        [Tooltip("A working character prefab. Its root components and the children listed below are copied.")]
        public GameObject sourceCharacter;
        [Tooltip("Direct children of the source to copy across (weapon rig, target ring, aim target).")]
        public string[] childrenToCopy = { "RigLayers", "SelectionIcon", "targetTransform" };

        [Header("Animation")]
        [Tooltip("Body Animator controller. Leave empty to keep the source's.")]
        public RuntimeAnimatorController bodyController;
        [Tooltip("Controller for the RigLayers Animator (needs equip_<weapon> states). Leave empty to keep the source's.")]
        public RuntimeAnimatorController rigController;

        [Header("Gameplay")]
        public Weapon startingWeapon;
        public CharacterClass characterClass = CharacterClass.HumanPlayer;
        public string tag = "Enemy";
        public string layer = "Enemy";

        [Header("Output")]
        public string outputFolder = "Assets/_Prefabs/Characters/Miners";
    }
}
