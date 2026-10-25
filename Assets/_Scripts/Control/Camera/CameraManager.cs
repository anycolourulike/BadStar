using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Rambler.Control;

//One camera that follows the active player and orbits 360 degrees around them.
//Rotation is driven by the HUD (CameraRotateButton, CameraRecentreButton).
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

    float targetYaw;
    float currentYaw;
    float rotateInput; //-1 left, 0 none, 1 right
    CinemachineTransposer transposer;

    public float Yaw => currentYaw;

    void Awake()
    {
        Instance = this;
        targetYaw = currentYaw = startYaw;
        SetUpFollowCam();
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
        if (activeCam == null) return;
        activeCam.Follow = newFollow;
        activeCam.LookAt = newFollow;
        var pc = newFollow.GetComponent<PlayerController>();
        if (pc != null) pc.cineMachine = activeCam;
    }

    //Hold-to-rotate input from the HUD buttons
    public void SetRotateInput(float direction)
    {
        rotateInput = Mathf.Clamp(direction, -1f, 1f);
    }

    public void Rotate(float degrees)
    {
        targetYaw += degrees;
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
