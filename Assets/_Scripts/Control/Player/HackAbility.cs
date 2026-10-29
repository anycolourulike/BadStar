using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Rambler.Attributes;
using Rambler.Ship;

namespace Rambler.Control
{
    //The player's hack ability. Tap the HUD button to arm it: hackable robots in range show their
    //target ring. Tap one and hold position for a moment; it then switches sides (see RobotHack)
    //and can be switched to like Rambler or Hayes.
    public class HackAbility : MonoBehaviour
    {
        //While armed, PlayerController leaves taps to this ability
        public static bool IsArmed { get; private set; }

        [SerializeField] PlayerSelector playerSelector;
        [SerializeField] Image buttonImage;
        [Tooltip("Radial fill over the button: hack progress, then cooldown.")]
        [SerializeField] Image progressFill;
        [SerializeField] float range = 15f;
        [SerializeField] float hackTime = 1.5f;
        [SerializeField] float cooldown = 20f;
        [SerializeField] Color readyColor = Color.white;
        [SerializeField] Color armedColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] Color busyColor = new Color(0.5f, 0.5f, 0.5f);

        readonly List<GameObject> ringsShown = new();
        GameObject target;
        float hackTimer;
        float cooldownLeft;

        void Start()
        {
            UpdateLook();
        }

        void OnDisable()
        {
            SetArmed(false);
            target = null;
        }

        //HUD button
        public void Toggle()
        {
            if (cooldownLeft > 0f || target != null) return;
            if (playerSelector != null && playerSelector.IsFull && !IsArmed)
            {
                Debug.Log("[Hack] Squad full (" + PlayerSelector.MaxPlayers + " units)");
                return;
            }
            SetArmed(!IsArmed);
        }

        void Update()
        {
            if (cooldownLeft > 0f)
            {
                cooldownLeft -= Time.deltaTime;
                SetFill(cooldownLeft / cooldown);
                if (cooldownLeft <= 0f) UpdateLook();
            }

            if (target != null)
            {
                Channel();
                return;
            }

            if (IsArmed && TryGetTap(out Vector2 screenPos)) PickTarget(screenPos);
        }

        void PickTarget(Vector2 screenPos)
        {
            GameObject picked = null;
            var ray = Camera.main.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null) picked = health.gameObject;
            }

            SetArmed(false);
            if (picked == null || !RobotHack.CanHack(picked) || !InRange(picked, range)) return;

            target = picked;
            hackTimer = 0f;
            ShowRing(target, true);
            UpdateLook();
        }

        void Channel()
        {
            //Break off if the robot dies, gets away, or someone else takes it
            if (!RobotHack.CanHack(target) || !InRange(target, range * 1.25f))
            {
                ShowRing(target, false);
                target = null;
                SetFill(0f);
                UpdateLook();
                return;
            }

            hackTimer += Time.deltaTime;
            SetFill(hackTimer / hackTime);
            if (hackTimer < hackTime) return;

            ShowRing(target, false);
            RobotHack.Convert(target, playerSelector);
            target = null;
            cooldownLeft = cooldown;
            UpdateLook();
        }

        void SetArmed(bool armed)
        {
            IsArmed = armed;
            foreach (var ring in ringsShown)
            {
                if (ring != null) ring.SetActive(false);
            }
            ringsShown.Clear();

            if (armed)
            {
                //Show which robots can be hacked from here
                foreach (var identity in FindObjectsOfType<CharacterIdentity>())
                {
                    if (RobotHack.CanHack(identity.gameObject) && InRange(identity.gameObject, range)) ShowRing(identity.gameObject, true);
                }
            }
            UpdateLook();
        }

        void ShowRing(GameObject character, bool show)
        {
            var ring = character.transform.Find("SelectionIcon");
            if (ring == null) return;
            ring.gameObject.SetActive(show);
            if (show && !ringsShown.Contains(ring.gameObject)) ringsShown.Add(ring.gameObject);
        }

        bool InRange(GameObject character, float distance)
        {
            var player = playerSelector != null ? playerSelector.activePlayer : null;
            if (player == null) return false;
            return (character.transform.position - player.transform.position).sqrMagnitude <= distance * distance;
        }

        //A new tap on the world (not the HUD); mouse too, for testing in the editor
        static bool TryGetTap(out Vector2 screenPos)
        {
            screenPos = default;
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Began) return false;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return false;
                screenPos = touch.position;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
                screenPos = Input.mousePosition;
                return true;
            }
            return false;
        }

        void UpdateLook()
        {
            if (buttonImage == null) return;
            buttonImage.color = IsArmed ? armedColor : (cooldownLeft > 0f || target != null) ? busyColor : readyColor;
            if (!IsArmed && target == null && cooldownLeft <= 0f) SetFill(0f);
        }

        void SetFill(float amount)
        {
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(amount);
        }
    }
}
