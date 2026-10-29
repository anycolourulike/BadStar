using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

namespace Rambler.UI
{
    //Fills the right-hand HUD panel with one portrait per unit on the player's side, from a template.
    //Up to PlayerSelector.MaxPlayers slots; a fallen unit keeps its slot (with the death mark)
    //until a new unit needs the room.
    public class PortraitPanel : MonoBehaviour
    {
        [SerializeField] PlayerSelector playerSelector;
        [Tooltip("An inactive PortraitSlot to copy; its position is where the first slot goes.")]
        [SerializeField] PortraitSlot template;
        [Tooltip("Offset from each slot to the next (negative y stacks downward).")]
        [SerializeField] Vector2 spacing = new Vector2(0f, -190f);

        readonly List<PortraitSlot> slots = new();

        void OnEnable()
        {
            if (playerSelector != null) playerSelector.PlayersChanged += Refresh;
        }

        void OnDisable()
        {
            if (playerSelector != null) playerSelector.PlayersChanged -= Refresh;
        }

        void Start()
        {
            if (template != null) template.gameObject.SetActive(false);
            Refresh();
        }

        public void Refresh()
        {
            if (playerSelector == null || template == null) return;

            //Drop slots whose character is gone from the scene
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i].Character == null) RemoveAt(i);
            }

            foreach (var character in playerSelector.players)
            {
                if (character == null || HasSlot(character)) continue;
                if (slots.Count >= PlayerSelector.MaxPlayers && !FreeADeadSlot()) break;
                var slot = Instantiate(template, template.transform.parent);
                slot.gameObject.SetActive(true);
                slot.Bind(character, playerSelector);
                slots.Add(slot);
            }
            Layout();
        }

        bool HasSlot(GameObject character)
        {
            foreach (var slot in slots)
            {
                if (slot.Character == character) return true;
            }
            return false;
        }

        //Make room by removing the oldest fallen unit
        bool FreeADeadSlot()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsDead && !playerSelector.players.Contains(slots[i].Character))
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        void RemoveAt(int index)
        {
            Destroy(slots[index].gameObject);
            slots.RemoveAt(index);
        }

        void Layout()
        {
            var origin = ((RectTransform)template.transform).anchoredPosition;
            for (int i = 0; i < slots.Count; i++)
            {
                ((RectTransform)slots[i].transform).anchoredPosition = origin + spacing * i;
            }
        }
    }
}
