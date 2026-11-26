using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Ship;

namespace Rambler.Crew.Battle
{
    //One room's patch of the Ship Systems screen: laid over that room in the top-down camera view. Tap it to select
    //the room; it shows the room's name and its alerts (fire, breach, intruders...). Copied by SystemsScreen.
    public class SystemsRoomZone : MonoBehaviour
    {
        public Button button;
        public Image fill;
        public Outline frame;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI alertText;

        public ShipRoom Room { get; private set; }
        float flash;

        public void Bind(ShipRoom room, System.Action<SystemsRoomZone> onTap)
        {
            Room = room;
            name = "Zone_" + room.roomName;
            if (nameText != null) nameText.text = room.roomName.ToUpperInvariant();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onTap(this));
        }

        //A shot just landed here
        public void Flash() => flash = 1f;

        public void Show(string alerts, Color tint, bool selected, float pulse)
        {
            if (alertText != null) alertText.text = alerts;
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 2f);
            if (fill != null)
            {
                var c = tint;
                c.a = Mathf.Lerp(tint.a, 0.7f, flash) * (0.75f + 0.25f * pulse);
                fill.color = Color.Lerp(c, new Color(1f, 1f, 1f, c.a), flash);
            }
            if (frame != null)
            {
                frame.effectColor = selected ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.35f);
                frame.effectDistance = selected ? new Vector2(5, -5) : new Vector2(2, -2);
            }
        }
    }
}
