using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Control;
using Rambler.Combat;

namespace Rambler.Ship
{
    //HUD panel shown when the player taps a door: which rooms it joins, its state and integrity, who controls it,
    //and what's on the far side (air, temperature, occupants once Sensors is hacked). Buttons open/close and
    //seal/unseal it once the Doors terminal is hacked, and Shoot Door makes the active player fire at it.
    public class DoorInfoPanel : MonoBehaviour
    {
        public static DoorInfoPanel Instance { get; private set; }

        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI status;
        [SerializeField] Image integrityFill;
        [SerializeField] TextMeshProUGUI integrityText;
        [SerializeField] TextMeshProUGUI control;
        [SerializeField] TextMeshProUGUI beyond;
        [SerializeField] TextMeshProUGUI atmosphere;
        [SerializeField] TextMeshProUGUI occupants;
        [SerializeField] Button openButton;
        [SerializeField] TextMeshProUGUI openLabel;
        [SerializeField] Button sealButton;
        [SerializeField] TextMeshProUGUI sealLabel;
        [SerializeField] Button shootButton;
        [SerializeField] TextMeshProUGUI shootLabel;
        [SerializeField] Button closeButton;
        [SerializeField] TextMeshProUGUI hint;
        [SerializeField] float refreshInterval = 0.25f;

        static readonly Color OpenColor = new Color(0.35f, 0.9f, 0.45f);
        static readonly Color ClosedColor = new Color(0.8f, 0.82f, 0.85f);
        static readonly Color SealedColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color BrokenColor = new Color(0.55f, 0.55f, 0.55f);
        static readonly Color WarnColor = new Color(1f, 0.45f, 0.3f);
        static readonly Color OkColor = new Color(0.85f, 0.9f, 0.95f);

        ShipDoor door;
        ShipDoorDamage damage;
        PlayerSelector playerSelector;
        float timer;

        public bool IsOpen => panel != null && panel.activeSelf;
        public ShipDoor Door => door;

        void Awake()
        {
            Instance = this;
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var playerCore = GameObject.Find("/PlayerCore");
            if (playerCore != null) playerSelector = playerCore.GetComponent<PlayerSelector>();
            if (openButton != null) openButton.onClick.AddListener(ToggleOpen);
            if (sealButton != null) sealButton.onClick.AddListener(ToggleSeal);
            if (shootButton != null) shootButton.onClick.AddListener(ShootDoor);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        public void Show(ShipDoor target)
        {
            if (target == null) return;
            door = target;
            damage = target.GetComponent<ShipDoorDamage>();
            panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            door = null;
            damage = null;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (door == null) { Hide(); return; }
            timer += Time.unscaledDeltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;
            Refresh();
        }

        void Refresh()
        {
            //With no ship AI (e.g. a wrecked Hopper) there is nothing to hack: the player works the doors and sees in directly
            var ship = ShipAI.Instance;
            bool hasDoors = ship == null || ship.PlayerHas(ShipControl.Doors);
            bool hasSensors = ship == null || ship.PlayerHas(ShipControl.Sensors);
            bool broken = door.IsBroken;
            var rooms = RoomsOf(door);

            title.text = rooms.Count == 0 && !string.IsNullOrEmpty(door.DisplayName) ? door.DisplayName
                       : rooms.Count == 2 ? rooms[0].roomName + "  <->  " + rooms[1].roomName
                       : rooms.Count == 1 ? rooms[0].roomName + " door" : "Door";

            //State
            string state; Color stateColor;
            if (broken) { state = "DESTROYED - stuck open"; stateColor = BrokenColor; }
            else if (door.State == ShipDoor.DoorState.Sealed) { state = ship != null ? "SEALED by the ship" : "SEALED"; stateColor = SealedColor; }
            else if (door.State == ShipDoor.DoorState.Open) { state = "OPEN"; stateColor = OpenColor; }
            else { state = "CLOSED"; stateColor = ClosedColor; }
            if (!broken && damage != null && damage.State == ShipDoorDamage.DamageState.Damaged) state += "  (damaged)";
            status.text = "● " + state;
            status.color = stateColor;

            //Integrity
            float max = damage != null ? damage.MaxHealth : 1f, hp = damage != null ? damage.Health : 1f;
            float frac = max > 0f ? Mathf.Clamp01(hp / max) : 0f;
            integrityFill.fillAmount = frac;
            integrityFill.color = Color.Lerp(SealedColor, OpenColor, frac);
            integrityText.text = damage != null ? Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(max) : "-";

            //Who runs the doors
            control.text = ship == null ? "Control: <color=#5BE57A>You</color>  (no ship AI)" : hasDoors ?"Control: <color=#5BE57A>You</color>" : "Control: <color=#FF5040>Ship</color>  (hack the Doors terminal)";

            //The room on the far side from the player
            var far = FarRoom(rooms);
            if (far == null)
            {
                beyond.text = "Beyond: outside";
                atmosphere.text = "";
                occupants.text = "";
            }
            else
            {
                beyond.text = "Beyond: " + far.roomName;
                bool badAir = far.Oxygen < 60f || far.IsVenting, cold = far.Temperature < 0f || far.IsFreezing;
                atmosphere.text = "O2 " + Mathf.RoundToInt(far.Oxygen) + "%" + (far.IsVenting ? " (venting)" : "") +
                                  "     Temp " + Mathf.RoundToInt(far.Temperature) + "°C" + (far.IsFreezing ? " (freezing)" : "");
                atmosphere.color = badAir || cold ? WarnColor : OkColor;
                occupants.text = hasSensors ? "Occupants: " + Describe(far) : "Occupants: unknown  (hack the Sensors terminal)";
            }

            //Buttons
            bool canControl = hasDoors && !broken;
            openButton.interactable = canControl && door.State != ShipDoor.DoorState.Sealed;
            openLabel.text = door.State == ShipDoor.DoorState.Open ? "CLOSE" : "OPEN";
            sealButton.interactable = canControl;
            sealLabel.text = door.State == ShipDoor.DoorState.Sealed ? "UNSEAL" : "SEAL";
            shootButton.interactable = !broken && door.State != ShipDoor.DoorState.Open && ActivePlayer() != null;
            var playerFighter = ActivePlayer() != null ? ActivePlayer().GetComponent<Fighter>() : null;
            bool melee = playerFighter != null && playerFighter.weaponConfig != null && playerFighter.weaponConfig.isMelee;
            if (shootLabel != null) shootLabel.text = melee ? "BREAK DOOR" : "SHOOT DOOR";
            FadeLabel(openButton, openLabel); FadeLabel(sealButton, sealLabel); FadeLabel(shootButton, shootLabel);
            hint.text = broken ? "This door can't be closed again." :
                        !hasDoors ? "Hack the Doors terminal to open or seal doors." :
                        door.State == ShipDoor.DoorState.Open ? "Open doors can't be shot." : "";
        }

        //Make unusable buttons read as unusable, not just slightly tinted
        static void FadeLabel(Button button, TextMeshProUGUI label)
        {
            if (button == null || label == null) return;
            label.alpha = button.interactable ? 1f : 0.35f;
        }

        void ToggleOpen()
        {
            if (door == null) return;
            if (door.State == ShipDoor.DoorState.Open) door.Close();
            else door.Open();
            Refresh();
        }

        void ToggleSeal()
        {
            if (door == null) return;
            door.SetSealed(door.State != ShipDoor.DoorState.Sealed);
            Refresh();
        }

        //Make the active player target the door's hitbox and open fire
        void ShootDoor()
        {
            var player = ActivePlayer();
            if (player == null || damage == null || damage.Hitbox == null) return;
            var target = damage.Hitbox.GetComponent<CombatTarget>();
            var fighter = player.GetComponent<Fighter>();
            if (target == null || fighter == null) return;
            fighter.CombatTarget = target;
            fighter.Attack(target.gameObject);
            fighter.HoldTarget = true;    //Keep at it after the finger lifts, until the next tap or the door gives way
            Hide();
        }

        GameObject ActivePlayer() => playerSelector != null ? playerSelector.activePlayer : null;

        ShipRoom FarRoom(List<ShipRoom> rooms)
        {
            if (rooms.Count == 0) return null;
            var player = ActivePlayer();
            if (player == null || rooms.Count == 1) return rooms.Count == 2 ? rooms[1] : null;
            Vector3 p = player.transform.position + Vector3.up;
            if (rooms[0].Contains(p)) return rooms[1];
            if (rooms[1].Contains(p)) return rooms[0];
            return Vector3.Distance(p, rooms[0].transform.position) > Vector3.Distance(p, rooms[1].transform.position) ? rooms[0] : rooms[1];
        }

        static List<ShipRoom> RoomsOf(ShipDoor target)
        {
            var list = new List<ShipRoom>();
            foreach (var room in ShipRoom.All)
            {
                foreach (var d in room.Doors)
                {
                    if (d == target) { list.Add(room); break; }
                }
            }
            return list;
        }

        static string Describe(ShipRoom room)
        {
            int crew = room.Count(CharacterType.Human, false), bots = room.Count(CharacterType.Robot, false), drones = room.Count(CharacterType.Drone, false);
            int security = room.Count(CharacterType.AtlasMK3, false) + room.Count(CharacterType.AtlasMK5, false);
            int allies = room.Count(CharacterType.Human, true) + room.Count(CharacterType.Robot, true) + room.Count(CharacterType.Drone, true) +
                         room.Count(CharacterType.AtlasMK3, true) + room.Count(CharacterType.AtlasMK5, true);
            var parts = new List<string>();
            if (crew > 0) parts.Add("<color=#FF5040>" + crew + " crew</color>");
            if (security > 0) parts.Add("<color=#FF5040>" + security + " security</color>");
            if (bots > 0) parts.Add("<color=#FF5040>" + bots + (bots == 1 ? " robot" : " robots") + "</color>");
            if (drones > 0) parts.Add("<color=#FF5040>" + drones + (drones == 1 ? " drone" : " drones") + "</color>");
            if (allies > 0) parts.Add("<color=#5BE57A>" + allies + (allies == 1 ? " ally" : " allies") + "</color>");
            return parts.Count > 0 ? string.Join(", ", parts) : "empty";
        }

        //Which door, if any, a tap ray lands on: a shut door's hitbox, or the upper part of an open doorway
        //(the lower part of an open doorway is left for tap-to-move)
        public static ShipDoor Pick(Ray ray)
        {
            float wallDist = float.MaxValue;
            if (Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                wallDist = hit.distance;
                var hitbox = hit.collider.GetComponent<ShipDoorHitbox>();
                if (hitbox != null) return hit.collider.GetComponentInParent<ShipDoor>();
            }
            ShipDoor best = null;
            float bestDist = float.MaxValue;
            foreach (var d in ShipDoor.All)
            {
                var t = d.transform;
                var plane = new Plane(t.forward, t.position);
                if (!plane.Raycast(ray, out float dist) || dist > wallDist + 0.3f || dist > bestDist) continue;
                Vector3 local = t.InverseTransformPoint(ray.GetPoint(dist));
                Vector2 size = d.OpeningSize;
                bool inWidth = Mathf.Abs(local.x) < size.x / 2f + 0.2f;
                bool upper = local.y > 0f && local.y < size.y / 2f + 0.3f;
                if (inWidth && upper) { best = d; bestDist = dist; }
            }
            return best;
        }
    }
}
