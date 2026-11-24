using UnityEngine;
using UnityEngine.EventSystems;

namespace Rambler.UI
{
    //The see-through layer over the Command screen's map: passes taps, drags and the mouse wheel to the screen.
    //Being UI, it also stops those taps reaching PlayerController (which ignores presses over the HUD).
    public class CommandTapArea : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        public CommandScreen screen;
        bool dragged;

        public void OnPointerDown(PointerEventData e) => dragged = false;

        public void OnBeginDrag(PointerEventData e) => dragged = true;

        public void OnDrag(PointerEventData e)
        {
            if (screen != null && Input.touchCount < 2) screen.Pan(e.delta);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!dragged && screen != null) screen.Tap(e.position);
        }

        public void OnScroll(PointerEventData e)
        {
            if (screen != null) screen.Zoom(-e.scrollDelta.y * 0.1f);
        }
    }
}
