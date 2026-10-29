using UnityEngine;
using UnityEngine.UI;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Ship;

namespace Rambler.UI
{
    //One HUD portrait for a unit on the player's side: picture, health, energy (if it has any),
    //death mark, and tap to take control. Built from the template by PortraitPanel.
    public class PortraitSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [Tooltip("The character picture; also the border PlayerController turns red while controlled.")]
        [SerializeField] Image portrait;
        [SerializeField] Image healthFill;
        [SerializeField] Image energyFill;
        [Tooltip("Hidden for units without an energy supply (e.g. hacked robots).")]
        [SerializeField] GameObject energyBar;
        [SerializeField] Image deathMark;

        PlayerSelector selector;
        Health health;

        public GameObject Character { get; private set; }
        public bool IsDead => health == null || health.IsDead();

        public void Bind(GameObject character, PlayerSelector playerSelector)
        {
            Character = character;
            selector = playerSelector;
            health = character.GetComponent<Health>();
            name = "Portrait_" + character.name;

            var identity = character.GetComponent<CharacterIdentity>();
            var controller = character.GetComponent<PlayerController>();
            Sprite picture = identity != null ? identity.Portrait : null;
            if (picture == null && controller != null) picture = controller.playerImg;
            if (picture != null) portrait.sprite = picture;

            //Characters with vitals drive the bars themselves; the rest just show health
            var vitals = character.GetComponent<PlayerVitals>();
            if (vitals != null) vitals.BindBars(healthFill, energyFill);
            if (energyBar != null) energyBar.SetActive(vitals != null);

            if (health != null) health.BindPortrait(button, deathMark);
            if (deathMark != null) deathMark.enabled = IsDead;

            //PlayerController colours this red while the unit is being controlled
            if (controller != null) controller.UIBorder = portrait;
            portrait.color = playerSelector != null && playerSelector.activePlayer == character ? Color.red : Color.white;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnPressed);
            button.enabled = !IsDead;
        }

        void OnPressed()
        {
            if (selector != null && Character != null) selector.SwitchTo(Character);
        }

        void Update()
        {
            if (health == null || healthFill == null) return;
            healthFill.fillAmount = health.GetFraction();
        }
    }
}
