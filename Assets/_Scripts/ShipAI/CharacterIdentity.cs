using UnityEngine;
using UnityEngine.Serialization;
using Rambler.Attributes;
using Rambler.Control;

namespace Rambler.Ship
{
    //What a character is (Human, Robot, Drone, Atlas suits, AI), whether the player can hack it,
    //and its side (from its Player/Companion/Enemy tag). Humans suffocate when their room's oxygen
    //is low and freeze when it gets cold; every other type is immune.
    [RequireComponent(typeof(Health))]
    public class CharacterIdentity : MonoBehaviour
    {
        public enum Hackability { ByType, Always, Never }

        [FormerlySerializedAs("kind")] //Was LifeForm.Kind: Organic became Human, Synthetic became Robot
        [SerializeField] CharacterType type = CharacterType.Human;
        [Tooltip("ByType: Robots and Drones can be hacked. Always/Never override that for this character.")]
        [SerializeField] Hackability hackable = Hackability.ByType;
        [Tooltip("Shown in the HUD portrait slot while this character is on the player's side.")]
        [SerializeField] Sprite portrait;

        [Header("Oxygen (Humans)")]
        [Tooltip("Oxygen % below which the character starts to suffocate.")]
        [SerializeField] float suffocateBelow = 40f;
        [Tooltip("Damage per second with no oxygen at all; scales down as oxygen rises to the threshold.")]
        [SerializeField] float suffocationDamage = 25f;

        [Header("Cold (Humans)")]
        [Tooltip("Temperature (C) below which the character starts to freeze.")]
        [SerializeField] float freezeBelow = 0f;
        [Tooltip("Temperature (C) at which cold does full damage.")]
        [SerializeField] float lethalCold = -60f;
        [SerializeField] float coldDamage = 30f;

        [SerializeField] float tickInterval = 0.5f;

        Health health;
        float timer;

        public CharacterType Type => type;
        public Sprite Portrait => portrait;
        public bool BreathesAir => BreathesAirType(type);
        public bool CanBeHacked => hackable == Hackability.Always || (hackable == Hackability.ByType && IsRoboticType(type));
        public CharacterRole Role => RoleOf(gameObject);
        public bool IsPlayerSide => Role == CharacterRole.Player || Role == CharacterRole.Companion;
        public ShipRoom CurrentRoom { get; private set; }
        public bool IsSuffocating { get; private set; }
        public bool IsFreezing { get; private set; }

        public static bool BreathesAirType(CharacterType t) => t == CharacterType.Human;
        public static bool IsRoboticType(CharacterType t) => t == CharacterType.Robot || t == CharacterType.Drone;

        public static CharacterRole RoleOf(GameObject character)
        {
            if (character.CompareTag("Player")) return CharacterRole.Player;
            if (character.CompareTag("Companion")) return CharacterRole.Companion;
            if (character.CompareTag("Enemy")) return CharacterRole.Enemy;
            return CharacterRole.Other;
        }

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < tickInterval) return;
            timer = 0f;

            //Sample at chest height so standing on the room's floor edge still counts
            CurrentRoom = ShipRoom.At(transform.position + Vector3.up);
            IsSuffocating = false;
            IsFreezing = false;
            if (!BreathesAir || CurrentRoom == null || health.IsDead()) return;

            float damagePerSecond = 0f;
            if (CurrentRoom.Oxygen < suffocateBelow)
            {
                IsSuffocating = true;
                damagePerSecond += suffocationDamage * (1f - CurrentRoom.Oxygen / suffocateBelow);
            }
            if (CurrentRoom.Temperature < freezeBelow)
            {
                IsFreezing = true;
                damagePerSecond += coldDamage * Mathf.Clamp01((freezeBelow - CurrentRoom.Temperature) / (freezeBelow - lethalCold));
            }
            if (damagePerSecond > 0f) health.TakeEnvironmentalDamage(damagePerSecond * tickInterval);
        }
    }
}
