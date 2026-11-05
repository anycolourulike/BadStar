using UnityEngine;
using UnityEngine.EventSystems;

//HUD joystick on the centre camera button.
//Drag left/right to slide the camera sideways across the map at the same height (the knob only slides sideways). A quick tap recentres the camera on the active player.
public class CameraJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] RectTransform knob;          //Moves with the finger, springs back on release
    [SerializeField] float maxRadius = 50f;       //Knob travel in canvas units
    [SerializeField] float deadZone = 0.15f;      //Ignore tiny wobbles around the centre
    [SerializeField] float tapMaxTime = 0.3f;     //A release this quick...
    [SerializeField] float tapMaxMove = 12f;      //...and this close to the start counts as a tap

    //True while a finger is on the joystick, so world taps (move/shoot) are ignored
    public static bool Active { get; private set; }

    RectTransform rect;
    Vector2 knobHome;
    Vector2 pressPos;
    float pressTime;
    float maxMove;
    int pointerId = int.MinValue;

    void Awake()
    {
        rect = (RectTransform)transform;
        if (knob != null) knobHome = knob.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerId != int.MinValue) return; //Already held by another finger
        pointerId = eventData.pointerId;
        Active = true;
        pressTime = Time.unscaledTime;
        maxMove = 0f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out pressPos);
        UpdateStick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        UpdateStick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        bool tap = Time.unscaledTime - pressTime <= tapMaxTime && maxMove <= tapMaxMove;
        Release();
        if (tap && CameraManager.Instance != null) CameraManager.Instance.RecentreOnActivePlayer();
    }

    void OnDisable()
    {
        Release();
    }

    void UpdateStick(PointerEventData eventData)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out local);
        maxMove = Mathf.Max(maxMove, (local - pressPos).magnitude);

        //Horizontal offset from the joystick centre only; the knob slides left/right and never moves up/down
        float x = Mathf.Clamp(local.x - rect.rect.center.x, -maxRadius, maxRadius);
        if (knob != null) knob.anchoredPosition = knobHome + new Vector2(x, 0f);

        float input = x / maxRadius;
        float m = Mathf.Abs(input);
        input = m < deadZone ? 0f : Mathf.Sign(input) * (m - deadZone) / (1f - deadZone); //Rescale so output starts at 0 just past the dead zone
        if (CameraManager.Instance != null) CameraManager.Instance.SetPanInput(input);
    }

    void Release()
    {
        pointerId = int.MinValue;
        Active = false;
        if (knob != null) knob.anchoredPosition = knobHome;
        if (CameraManager.Instance != null) CameraManager.Instance.SetPanInput(0f);
    }
}
