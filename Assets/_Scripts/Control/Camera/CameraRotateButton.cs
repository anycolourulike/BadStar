using UnityEngine;
using UnityEngine.EventSystems;

//HUD button that orbits the camera while held
public class CameraRotateButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float direction = 1f; //-1 left, 1 right

    public void OnPointerDown(PointerEventData eventData)
    {
        if (CameraManager.Instance != null) CameraManager.Instance.SetRotateInput(direction);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    void OnDisable()
    {
        Release();
    }

    void Release()
    {
        if (CameraManager.Instance != null) CameraManager.Instance.SetRotateInput(0f);
    }
}
