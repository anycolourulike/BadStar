using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Control;
using Rambler.Ship;

namespace Rambler.UI
{
    //One room on the ship map: coloured by its air, with its oxygen, temperature and (once the
    //player has the sensors) who is inside. Tap to select it. Built from a template by ShipMapPanel.
    public class ShipMapRoom : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image fill;
        [Tooltip("Shown while this room is selected.")]
        [SerializeField] Graphic selectedOutline;
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] Color normalColor = new Color(0.16f, 0.24f, 0.32f, 0.9f);
        [SerializeField] Color noAirColor = new Color(0.55f, 0.12f, 0.1f, 0.9f);
        [SerializeField] Color frozenColor = new Color(0.75f, 0.9f, 1f, 0.9f);

        public ShipRoom Room { get; private set; }

        public void Bind(ShipRoom room, Action<ShipMapRoom> onTap)
        {
            Room = room;
            name = "Room_" + room.roomName;
            if (nameText != null) nameText.text = room.roomName;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onTap(this));
        }

        public void Refresh(bool showOccupants, bool selected)
        {
            if (Room == null) return;
            if (selectedOutline != null) selectedOutline.enabled = selected;

            //Grey-red as the oxygen goes, pale as it freezes
            var color = Color.Lerp(noAirColor, normalColor, Room.Oxygen / 100f);
            if (Room.Temperature < 0f) color = Color.Lerp(color, frozenColor, Mathf.Clamp01(-Room.Temperature / 60f));
            fill.color = color;

            if (statusText == null) return;
            string status = "O2 " + Room.Oxygen.ToString("0") + "%  " + Room.Temperature.ToString("0") + "°C";
            if (AnySealed(Room)) status += "\n<color=#FFB040>SEALED</color>";
            //Who is inside comes through the room's camera, so a broken one hides it
            if (!Room.HasWorkingCamera) status += "\n<color=#888888>CAMERA OFFLINE</color>";
            else if (showOccupants)
            {
                string enemies = TypeCounts(Room, playerSide: false);
                string squad = TypeCounts(Room, playerSide: true);
                if (enemies.Length > 0) status += "\n<color=#FF5A4A>Enemy: " + enemies + "</color>";
                if (squad.Length > 0) status += "\n<color=#7CFF8A>Squad: " + squad + "</color>";
            }
            statusText.text = status;
        }

        public static bool AnySealed(ShipRoom room)
        {
            foreach (var door in room.Doors)
            {
                if (door != null && door.IsSealed) return true;
            }
            return false;
        }

        //"2 human, 1 robot" for one side; empty when nobody of that side is inside
        static string TypeCounts(ShipRoom room, bool playerSide)
        {
            string text = "";
            foreach (var type in new[] { CharacterType.Human, CharacterType.Robot, CharacterType.Drone })
            {
                int count = room.Count(type, playerSide);
                if (count == 0) continue;
                if (text.Length > 0) text += ", ";
                text += count + " " + type.ToString().ToLower();
            }
            return text;
        }
    }
}
