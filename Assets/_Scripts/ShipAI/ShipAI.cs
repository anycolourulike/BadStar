using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //The ship's brain, one per level. Sensors feed it a detection level; as that rises it moves
    //Dormant > Suspicious > Alert > Lockdown and responds: trapping the player's room, slamming
    //doors on them, and sealing the whole ship. Hacking terminals (later phase) hands it to the player.
    public class ShipAI : MonoBehaviour
    {
        public static ShipAI Instance { get; private set; }
        public static event Action<AlertState> AlertChanged;
        public static event Action<ShipFaction> ControllerChanged;

        [Header("Detection")]
        [SerializeField] float suspiciousAt = 25f;
        [SerializeField] float alertAt = 60f;
        [Tooltip("Detection points lost per second once no sensor has seen a player for a moment.")]
        [SerializeField] float detectionDecay = 6f;
        [Tooltip("Seconds without a sighting before detection starts to fall.")]
        [SerializeField] float forgetDelay = 3f;
        [Tooltip("Seconds spent at Alert before the ship goes into Lockdown.")]
        [SerializeField] float lockdownAfter = 20f;

        [Header("Responses")]
        [SerializeField] float decisionInterval = 1f;
        [Tooltip("Seal the doors of the room the player was last seen in.")]
        [SerializeField] bool trapPlayerRoom = true;
        [SerializeField] float trapCooldown = 25f;
        [Tooltip("How long a trapped room stays sealed (outside Lockdown).")]
        [SerializeField] float trapDuration = 12f;
        [Tooltip("Slam doors on player characters who step into a doorway while the ship is alerted.")]
        [SerializeField] bool crushDoors = true;
        [SerializeField] float crushCooldown = 8f;

        ShipFaction controller = ShipFaction.Hostile;
        AlertState alert = AlertState.Dormant;
        float detection;
        float timeSinceSighting = Mathf.Infinity;
        float timeAtAlert;
        float decisionTimer;
        float trapReadyAt;
        float crushReadyAt;
        readonly HashSet<ShipDoor> sealedByShip = new();

        public ShipFaction Controller => controller;
        public AlertState Alert => alert;
        public float Detection => detection;
        public ShipRoom LastKnownRoom { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public GameObject LastSeenTarget { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ShipAI] More than one ShipAI in the scene; ignoring " + name);
                enabled = false;
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            ShipDoor.DoorwayEntered += OnDoorwayEntered;
        }

        void OnDisable()
        {
            ShipDoor.DoorwayEntered -= OnDoorwayEntered;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (controller != ShipFaction.Hostile) return;

            timeSinceSighting += Time.deltaTime;
            if (timeSinceSighting > forgetDelay)
            {
                detection = Mathf.Max(0f, detection - detectionDecay * Time.deltaTime);
            }
            UpdateAlertState();

            decisionTimer += Time.deltaTime;
            if (decisionTimer >= decisionInterval)
            {
                decisionTimer = 0f;
                Decide();
            }
        }

        //Called by sensors each time they see a player character
        public void ReportSighting(ShipSensor sensor, GameObject target, float amount)
        {
            if (controller != ShipFaction.Hostile) return;
            detection = Mathf.Min(100f, detection + amount);
            timeSinceSighting = 0f;
            LastSeenTarget = target;
            LastKnownPosition = target.transform.position;
            LastKnownRoom = ShipRoom.At(LastKnownPosition) ?? sensor.Room;
        }

        void UpdateAlertState()
        {
            if (alert >= AlertState.Alert) timeAtAlert += Time.deltaTime;
            else timeAtAlert = 0f;

            if (alert == AlertState.Alert && timeAtAlert >= lockdownAfter) SetAlert(AlertState.Lockdown);
            else if (detection >= alertAt && alert < AlertState.Alert) SetAlert(AlertState.Alert);
            else if (detection >= suspiciousAt && alert < AlertState.Suspicious) SetAlert(AlertState.Suspicious);
            //Calm down in steps, so the ship stays wary for a while after losing the player
            else if (detection < suspiciousAt && alert >= AlertState.Alert) SetAlert(AlertState.Suspicious);
            else if (detection <= 0f && alert == AlertState.Suspicious) SetAlert(AlertState.Dormant);
        }

        void SetAlert(AlertState next)
        {
            if (next == alert) return;
            alert = next;
            Debug.Log("[ShipAI] " + alert + " (detection " + detection.ToString("F0") + ")");

            if (alert == AlertState.Lockdown) SealEveryDoor();
            if (alert <= AlertState.Suspicious) ReleaseSeals();
            AlertChanged?.Invoke(alert);
        }

        void Decide()
        {
            if (alert < AlertState.Alert) return;
            if (trapPlayerRoom && Time.time >= trapReadyAt && LastKnownRoom != null && LastKnownRoom.HasPlayerSide())
            {
                SealRoom(LastKnownRoom, trapDuration);
                trapReadyAt = Time.time + trapCooldown;
            }
        }

        void OnDoorwayEntered(ShipDoor door, GameObject character)
        {
            if (!crushDoors || controller != ShipFaction.Hostile || alert < AlertState.Alert) return;
            if (!ShipCharacters.IsPlayerSide(character) || Time.time < crushReadyAt) return;
            //Give them a moment to step fully into the doorway
            StartCoroutine(CrushAfter(door, 0.35f));
            crushReadyAt = Time.time + crushCooldown;
        }

        IEnumerator CrushAfter(ShipDoor door, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (door == null) yield break;
            int hit = door.Crush();
            if (hit > 0) Debug.Log("[ShipAI] Crushed " + hit + " in " + door.name);
        }

        // ---------- Ship actions; the player will use these too after takeover ----------

        //Seals every door of a room. Duration 0 keeps it sealed until released.
        public void SealRoom(ShipRoom room, float duration)
        {
            foreach (var door in room.Doors)
            {
                if (door == null) continue;
                door.SetSealed(true);
                sealedByShip.Add(door);
            }
            Debug.Log("[ShipAI] Sealed " + room.roomName);
            if (duration > 0f) StartCoroutine(UnsealAfter(room, duration));
        }

        public void UnsealRoom(ShipRoom room)
        {
            foreach (var door in room.Doors)
            {
                if (door == null || !sealedByShip.Remove(door)) continue;
                door.SetSealed(false);
            }
        }

        IEnumerator UnsealAfter(ShipRoom room, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (alert != AlertState.Lockdown && room != null) UnsealRoom(room);
        }

        void SealEveryDoor()
        {
            foreach (var room in ShipRoom.All)
            {
                foreach (var door in room.Doors)
                {
                    if (door == null) continue;
                    door.SetSealed(true);
                    sealedByShip.Add(door);
                }
            }
        }

        //Opens every door the ship itself sealed (not doors a designer placed sealed)
        void ReleaseSeals()
        {
            foreach (var door in sealedByShip)
            {
                if (door != null) door.SetSealed(false);
            }
            sealedByShip.Clear();
        }

        //Terminal takeover (later phase) calls this to hand the ship over
        public void SetController(ShipFaction faction)
        {
            if (faction == controller) return;
            controller = faction;
            StopAllCoroutines();
            if (controller == ShipFaction.Player)
            {
                detection = 0f;
                SetAlert(AlertState.Dormant);
                ReleaseSeals();
            }
            Debug.Log("[ShipAI] Now controlled by " + controller);
            ControllerChanged?.Invoke(controller);
        }
    }
}
