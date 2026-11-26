using UnityEngine;
using UnityEngine.EventSystems;

namespace Rambler.Crew.Battle
{
    //Sits on a tactical map image and reports taps on it as a point on the image (0-1 across, 0-1 up), which the
    //screen turns into a spot on the map. Used by the Navigation screen to set a course.
    public class TacticalMapTap : MonoBehaviour, IPointerClickHandler
    {
        public System.Action<Vector2> Tapped;

        public void OnPointerClick(PointerEventData e)
        {
            var r = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out var local)) return;
            var rect = r.rect;
            Tapped?.Invoke(new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
        }
    }
}
