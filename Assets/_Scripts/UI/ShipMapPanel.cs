using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Ship;

namespace Rambler.UI
{
    //The ship map: a top-down plan of every ShipRoom, opened from the HUD once the player has hacked
    //a terminal. Select a room, then use the systems taken so far: seal, open or crush its doors
    //(Doors), vent, freeze or restore its air (Life Support). Sensors show who is in each room.
    public class ShipMapPanel : MonoBehaviour
    {
        [SerializeField] GameObject window;
        [Tooltip("HUD button that opens the map; hidden until the player holds a ship system.")]
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform mapArea;
        [Tooltip("An inactive ShipMapRoom to copy for each room.")]
        [SerializeField] ShipMapRoom roomTemplate;
        [SerializeField] float mapPadding = 16f;
        [SerializeField] float minTileSize = 90f;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI selectedText;

        [Header("Actions")]
        [SerializeField] Button sealButton;
        [SerializeField] Button openDoorsButton;
        [SerializeField] Button crushButton;
        [SerializeField] Button ventButton;
        [SerializeField] Button freezeButton;
        [SerializeField] Button restoreButton;

        [SerializeField] float refreshInterval = 0.25f;

        readonly List<ShipMapRoom> tiles = new();
        ShipMapRoom selected;
        string lastAction = "";
        float timer;

        void Start()
        {
            if (window != null) window.SetActive(false);
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            Wire(sealButton, () => Act(ShipAI.Instance.PlayerSeal(selected.Room, true), "Doors sealed"));
            Wire(openDoorsButton, () => Act(ShipAI.Instance.PlayerSeal(selected.Room, false), "Doors released"));
            Wire(crushButton, () =>
            {
                int hit = ShipAI.Instance.PlayerCrush(selected.Room);
                lastAction = hit > 0 ? "Crushed " + hit : "No enemy in a doorway";
            });
            Wire(ventButton, () => Act(ShipAI.Instance.PlayerVent(selected.Room), "Venting oxygen"));
            Wire(freezeButton, () => Act(ShipAI.Instance.PlayerFreeze(selected.Room), "Freezing"));
            Wire(restoreButton, () => Act(ShipAI.Instance.PlayerRestore(selected.Room), "Restoring air"));
            UpdateOpenButton();
        }

        void OnEnable()
        {
            ShipAI.ControlGranted += OnControlGranted;
            ShipAI.ControllerChanged += OnControllerChanged;
        }

        void OnDisable()
        {
            ShipAI.ControlGranted -= OnControlGranted;
            ShipAI.ControllerChanged -= OnControllerChanged;
        }

        void OnControlGranted(ShipControl system) => UpdateOpenButton();
        void OnControllerChanged(ShipFaction faction) => UpdateOpenButton();

        void UpdateOpenButton()
        {
            if (openButton != null) openButton.gameObject.SetActive(HasAnySystem());
        }

        static bool HasAnySystem()
        {
            var ship = ShipAI.Instance;
            if (ship == null) return false;
            foreach (ShipControl system in System.Enum.GetValues(typeof(ShipControl)))
            {
                if (ship.PlayerHas(system)) return true;
            }
            return false;
        }

        public void Open()
        {
            if (window == null || ShipAI.Instance == null) return;
            window.SetActive(true);
            BuildTiles();
            lastAction = "";
            Refresh();
        }

        public void Close()
        {
            if (window != null) window.SetActive(false);
        }

        void Update()
        {
            if (window == null || !window.activeSelf) return;
            timer += Time.unscaledDeltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;
            Refresh();
        }

        //One tile per room, laid out as a top-down plan (x right, z up) scaled to fit the map area
        void BuildTiles()
        {
            var rooms = ShipRoom.All;
            if (tiles.Count == rooms.Count && tiles.TrueForAll(t => t != null && t.Room != null)) return;

            foreach (var tile in tiles)
            {
                if (tile != null) Destroy(tile.gameObject);
            }
            tiles.Clear();
            if (rooms.Count == 0 || roomTemplate == null) return;

            var bounds = new List<Bounds>();
            Bounds all = RoomBounds(rooms[0]);
            foreach (var room in rooms)
            {
                var b = RoomBounds(room);
                bounds.Add(b);
                all.Encapsulate(b);
            }

            var area = mapArea.rect;
            float width = Mathf.Max(0.01f, all.size.x), depth = Mathf.Max(0.01f, all.size.z);
            float scale = Mathf.Min((area.width - mapPadding * 2f) / width, (area.height - mapPadding * 2f) / depth);

            for (int i = 0; i < rooms.Count; i++)
            {
                var tile = Instantiate(roomTemplate, mapArea);
                tile.gameObject.SetActive(true);
                tile.Bind(rooms[i], Select);
                var rect = (RectTransform)tile.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                var offset = bounds[i].center - all.center;
                rect.anchoredPosition = new Vector2(offset.x, offset.z) * scale;
                rect.sizeDelta = new Vector2(Mathf.Max(minTileSize, bounds[i].size.x * scale - 4f), Mathf.Max(minTileSize, bounds[i].size.z * scale - 4f));
                tiles.Add(tile);
            }
            if (selected == null || !tiles.Contains(selected)) selected = tiles.Count > 0 ? tiles[0] : null;
        }

        static Bounds RoomBounds(ShipRoom room)
        {
            var box = room.GetComponent<BoxCollider>();
            return box != null ? box.bounds : new Bounds(room.transform.position, Vector3.one * 4f);
        }

        void Select(ShipMapRoom tile)
        {
            selected = tile;
            lastAction = "";
            Refresh();
        }

        void Refresh()
        {
            var ship = ShipAI.Instance;
            if (ship == null) return;
            bool doors = ship.PlayerHas(ShipControl.Doors);
            bool air = ship.PlayerHas(ShipControl.LifeSupport);
            bool sensors = ship.PlayerHas(ShipControl.Sensors);

            foreach (var tile in tiles)
            {
                if (tile != null) tile.Refresh(sensors, tile == selected);
            }

            if (titleText != null)
            {
                int hacked = 0;
                foreach (var terminal in ShipTerminal.All)
                {
                    if (terminal.IsHacked) hacked++;
                }
                titleText.text = ship.Controller == ShipFaction.Player
                    ? "SHIP CONTROL — <color=#7CFF8A>YOURS</color>"
                    : "SHIP CONTROL — terminals " + hacked + "/" + ShipTerminal.All.Count;
            }

            var room = selected != null ? selected.Room : null;
            bool hasRoom = room != null;
            bool hasDoors = hasRoom && room.Doors.Count > 0;
            bool sealedNow = hasRoom && ShipMapRoom.AnySealed(room);
            SetInteractable(sealButton, doors && hasDoors && !sealedNow);
            SetInteractable(openDoorsButton, doors && sealedNow);
            SetInteractable(crushButton, doors && hasDoors);
            SetInteractable(ventButton, air && hasRoom && !room.IsVenting);
            SetInteractable(freezeButton, air && hasRoom && !room.IsFreezing);
            SetInteractable(restoreButton, air && hasRoom && (room.IsVenting || room.IsFreezing));

            if (selectedText == null) return;
            if (!hasRoom)
            {
                selectedText.text = "Select a room";
                return;
            }
            string text = "<b>" + room.roomName + "</b>";
            if (!doors) text += "\n<color=#999999>Doors: hack a Doors terminal</color>";
            if (!air) text += "\n<color=#999999>Air: hack a Life Support terminal</color>";
            if (!sensors) text += "\n<color=#999999>Occupants: hack a Sensors terminal</color>";
            if (air && room.HasBreather(playerSide: true)) text += "\n<color=#FF5A4A>Your squad has humans in here!</color>";
            if (lastAction.Length > 0) text += "\n\n" + lastAction;
            selectedText.text = text;
        }

        void Act(bool done, string message)
        {
            lastAction = done ? message : "Not available";
            Refresh();
        }

        void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(() =>
            {
                if (selected == null || selected.Room == null || ShipAI.Instance == null) return;
                action();
                Refresh();
            });
        }

        static void SetInteractable(Button button, bool on)
        {
            if (button != null) button.interactable = on;
        }
    }
}
