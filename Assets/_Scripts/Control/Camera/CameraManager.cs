using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Rambler.Control;

//One camera that follows the active player and orbits 360 degrees around them.
//The HUD arrows (CameraRotateButton) rotate it; the centre joystick (CameraJoystick) slides it across the map (sideways and up/down the screen)
//at the same height, and a tap on the joystick brings it back to the active player.
//Runs before the Cinemachine brain so the pivot has moved before the camera is placed this frame.
[DefaultExecutionOrder(-100)]
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    public List<CinemachineVirtualCamera> vCams = new();
    public List<GameObject> players = new();
    public CinemachineVirtualCamera activeCam; //The follow camera
    public GameObject activePlayer; //set from PlayerSelector
    public GameObject companion;

    [Header("Orbit")]
    [SerializeField] float distance = 53f;      //Horizontal distance from the player
    [SerializeField] float height = 40f;        //Height above the player
    [SerializeField] float startYaw = 39f;      //Heading the camera starts at and recentres to
    [SerializeField] float rotateSpeed = 120f;  //Degrees per second while a rotate button is held
    [SerializeField] float smoothing = 10f;

    [Header("Joystick pan")]
    [SerializeField] float panSpeed = 30f;      //Metres per second at full joystick
    [SerializeField] float maxPan = 80f;        //How far the view can slide away from the player

    float targetYaw;
    float currentYaw;
    float rotateInput; //-1 left, 0 none, 1 right
    Vector2 panInput; //x: screen sideways (-1 left .. 1 right), y: screen up/down (1 = up the screen, away from the camera)
    Vector3 targetPan, currentPan; //World-space slide of the view away from the player, flat (y = 0)
    Transform followTarget; //The player the camera is on
    Transform pivot;        //What the camera actually follows and looks at: player + pan
    CinemachineTransposer transposer;

    public float Yaw => currentYaw;

    void Awake()
    {
        Instance = this;
        targetYaw = currentYaw = startYaw;
        pivot = new GameObject("CameraPivot").transform;
        if (activeCam != null && activeCam.Follow != null) followTarget = activeCam.Follow;
        SetUpFollowCam();
        if (followTarget != null) UpdateFollow(followTarget);
    }

    public void OnEnable()
    {
        PlayerSelector.OnPlayerObjectChanged += UpdatePlayer;
    }

    public void OnDisable()
    {
        PlayerSelector.OnPlayerObjectChanged -= UpdatePlayer;
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (transposer == null) return;
        if (rotateInput != 0f)
        {
            targetYaw += rotateInput * rotateSpeed * Time.unscaledDeltaTime;
        }
        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, smoothing * Time.unscaledDeltaTime);
        transposer.m_FollowOffset = OffsetForYaw(currentYaw);

        if (panInput != Vector2.zero)
        {
            //Slide along the screen's sideways and up/down directions, kept flat so the height never changes
            Quaternion heading = Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 right = heading * Vector3.left;    //Screen right on the ground
            Vector3 forward = heading * Vector3.back;  //Screen up on the ground (the way the camera faces)
            Vector3 move = right * panInput.x + forward * panInput.y;
            targetPan = Vector3.ClampMagnitude(targetPan + move * (panSpeed * Time.unscaledDeltaTime), maxPan);
        }
        currentPan = Vector3.Lerp(currentPan, targetPan, smoothing * Time.unscaledDeltaTime);
        if (followTarget != null) pivot.position = followTarget.position + currentPan;
    }

    //Swap the follow camera's body for a world-space transposer we can orbit
    void SetUpFollowCam()
    {
        if (activeCam == null) return;
        transposer = activeCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
        {
            transposer = activeCam.AddCinemachineComponent<CinemachineTransposer>();
        }
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        //No damping, so the camera pivots exactly around the player instead of lagging behind the orbit
        transposer.m_XDamping = transposer.m_YDamping = transposer.m_ZDamping = 0f;
        transposer.m_FollowOffset = OffsetForYaw(currentYaw);

        //Hard look-at keeps the player dead centre while the camera turns
        if (activeCam.GetCinemachineComponent<CinemachineHardLookAt>() == null)
        {
            activeCam.AddCinemachineComponent<CinemachineHardLookAt>();
        }

        //The follow camera always wins
        foreach (CinemachineVirtualCamera VC in vCams)
        {
            if (VC != null && VC != activeCam) VC.Priority = 0;
        }
        activeCam.Priority = 2;
    }

    Vector3 OffsetForYaw(float yaw)
    {
        return Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, height, distance);
    }

    public void UpdatePlayer(GameObject newPlayer)
    {
        activePlayer = newPlayer;
        UpdateFollow(newPlayer.transform);
    }

    public void UpdateFollow(Transform newFollow)
    {
        if (activeCam == null || newFollow == null) return;
        followTarget = newFollow;
        pivot.position = followTarget.position + currentPan;
        activeCam.Follow = pivot;
        activeCam.LookAt = pivot;
        var pc = newFollow.GetComponent<PlayerController>();
        if (pc != null) pc.cineMachine = activeCam;
    }

    //Hold-to-rotate input from the HUD buttons
    public void SetRotateInput(float direction)
    {
        rotateInput = Mathf.Clamp(direction, -1f, 1f);
    }

    //Joystick input from the centre HUD button: x slides the view sideways, y slides it up/down the screen (each -1..1)
    public void SetPanInput(Vector2 input)
    {
        panInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void Rotate(float degrees)
    {
        targetYaw += degrees;
    }

    //Centre button tap: slide the view back onto whichever player is active (the heading is left to the arrows)
    public void RecentreOnActivePlayer()
    {
        var selector = FindFirstObjectByType<PlayerSelector>();
        if (selector != null && selector.activePlayer != null) activePlayer = selector.activePlayer;
        if (activePlayer != null) UpdateFollow(activePlayer.transform);
        if (activeCam != null)
        {
            //The follow camera wins again even if a trigger camera took over
            foreach (CinemachineVirtualCamera VC in vCams)
            {
                if (VC != null && VC != activeCam) VC.Priority = 0;
            }
            activeCam.Priority = 2;
        }
        targetPan = Vector3.zero;
    }

    public void ResetRotation()
    {
        //Take the short way back to the start heading
        targetYaw = currentYaw + Mathf.DeltaAngle(currentYaw, startYaw);
    }

    //Old trigger-based camera switching; the camera now stays on the player
    public void SwitchPriority(CinemachineVirtualCamera newVC)
    {
    }
}
