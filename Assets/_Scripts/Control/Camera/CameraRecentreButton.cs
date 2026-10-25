using UnityEngine;
using UnityEngine.EventSystems;

//HUD button that swings the camera back to its starting heading
public class CameraRecentreButton : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (CameraManager.Instance != null) CameraManager.Instance.ResetRotation();
    }
}
