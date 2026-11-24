using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Crew.UI
{
    //One card or row in the Crew screen (a recruit, a crew member, a skill, a weapon, a course).
    //The screen copies an inactive template and fills in whichever of these parts the template has.
    public class CrewUIItem : MonoBehaviour
    {
        public Image portrait;
        [Tooltip("Initials shown on the portrait when the character has no picture.")]
        public TextMeshProUGUI initials;
        public TextMeshProUGUI title;
        public TextMeshProUGUI body;
        public TextMeshProUGUI side;
        [Tooltip("Filled bar (Image type Filled), e.g. a skill level.")]
        public Image bar;
        public Image highlight;
        [Tooltip("Tapping the whole item (rows that select something).")]
        public Button select;
        public Button action;
        public TextMeshProUGUI actionLabel;
        public Button action2;
        public TextMeshProUGUI action2Label;

        public void SetPortrait(Sprite sprite, string name)
        {
            if (portrait != null)
            {
                portrait.sprite = sprite;
                portrait.color = sprite != null ? Color.white : PortraitColor(name);
            }
            if (initials != null)
            {
                initials.gameObject.SetActive(sprite == null);
                initials.text = Initials(name);
            }
        }

        public static void SetText(TextMeshProUGUI t, string text)
        {
            if (t != null) t.text = text;
        }

        public static void SetButton(Button b, TextMeshProUGUI label, string text, bool show, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            if (b == null) return;
            b.gameObject.SetActive(show);
            b.interactable = interactable;
            b.onClick.RemoveAllListeners();
            if (onClick != null) b.onClick.AddListener(onClick);
            if (label != null) label.text = text;
        }

        static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Split(' ');
            return parts.Length > 1 && parts[1].Length > 0 ? (parts[0][0].ToString() + parts[1][0]).ToUpperInvariant() : name.Substring(0, Mathf.Min(2, name.Length)).ToUpperInvariant();
        }

        //A steady muted colour per name, so faceless recruits are still told apart at a glance
        static Color PortraitColor(string name)
        {
            int h = 17;
            foreach (char c in name ?? "") h = h * 31 + c;
            return Color.HSVToRGB(Mathf.Abs(h % 360) / 360f, 0.45f, 0.55f);
        }
    }
}
