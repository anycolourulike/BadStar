using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //The ship's brain, one per level. Sensors feed it a detection level; as that rises it moves
    //Dormant > Suspicious > Alert > Lockdown and responds: trapping the player's room, venting or
    //freezing it when a human is inside, slamming doors on them, and sealing the whole ship.
    //Each hacked ShipTerminal hands one system (doors, life support, sensors) to the player, who
    //then uses it from the ship map; once every terminal is hacked the whole ship changes sides.
    public class ShipAI : MonoBehaviour
    {
        public static ShipAI Instance { get; private set; }
        public static event Action<AlertState> AlertChanged;
        public static event Action<ShipFaction> ControllerChanged;
        public static event Action<ShipControl> ControlGranted;

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

        [Header("Life Support")]
        [Tooltip("Seal and vent or freeze a room holding a human player character. Alternates vent and freeze.")]
        [SerializeField] bool attackLifeSupport = true;
        [SerializeField] float lifeSupportCooldown = 30f;
        [Tooltip("How long the room stays sealed and vented/frozen before the ship lets the air back.")]
        [SerializeField] float lifeSupportDuration = 20f;

        ShipFaction controller = ShipFaction.Hostile;
        AlertState alert = AlertState.Dormant;
        float detection;
        float timeSinceSighting = Mathf.Infinity;
        float timeAtAlert;
        float decisionTimer;
        float trapReadyAt;
        float crushReadyAt;
        float lifeSupportReadyAt;
        bool freezeNext;
        readonly HashSet<ShipDoor> sealedByShip = new();
        readonly HashSet<ShipRoom> airChangedByShip = new();
        readonly HashSet<ShipControl> playerControls = new();

        public ShipFaction Controller => controller;
        //True once the player has hacked the system (or taken the whole ship)
        public bool PlayerHas(ShipControl system) => controller == ShipFaction.Player || playerControls.Contains(system);
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
            if (controller != ShipFaction.Hostile || PlayerHas(ShipControl.Sensors)) return;
            detection = Mathf.Min(100f, detection + amount);
            timeSinceSighting = 0f;
            LastSeenTarget = target;
            LastKnownPosition = target.transform.position;
            LastKnownRoom = ShipRoom.At(LastKnownPosition) ?? (sensor != null ? sensor.Room : null); //No sensor: a terminal alarm
        }

        //Something the ship can't miss (a camera destroyed): straight to Alert, with the trouble's
        //location as the last known position. A ship blinded by a hacked Sensors terminal never hears it.
        public void RaiseAlarm(Vector3 position, ShipRoom room, string reason)
        {
            if (controller != ShipFaction.Hostile || PlayerHas(ShipControl.Sensors)) return;
            detection = Mathf.Max(detection, alertAt + 10f);
            timeSinceSighting = 0f;
            LastKnownPosition = position;
            LastKnownRoom = room;
            Debug.Log("[ShipAI] Alarm: " + reason + (room != null ? " in " + room.roomName : ""));
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

            if (alert == AlertState.Lockdown && !PlayerHas(ShipControl.Doors)) SealEveryDoor();
            if (alert <= AlertState.Suspicious)
            {
                ReleaseSeals();
                RestoreAllAir();
            }
            AlertChanged?.Invoke(alert);
        }

        void Decide()
        {
            if (alert < AlertState.Alert || LastKnownRoom == null) return;

            //Humans can be killed with the air itself; robots only get trapped
            bool canUseDoors = !PlayerHas(ShipControl.Doors);
            if (attackLifeSupport && canUseDoors && !PlayerHas(ShipControl.LifeSupport) &&
                Time.time >= lifeSupportReadyAt && LastKnownRoom.HasBreather(playerSide: true))
            {
                StartCoroutine(AttackLifeSupport(LastKnownRoom, freezeNext));
                freezeNext = !freezeNext;
                lifeSupportReadyAt = Time.time + lifeSupportCooldown;
                trapReadyAt = Time.time + trapCooldown; //The room is sealed for this already
                return;
            }

            if (trapPlayerRoom && canUseDoors && Time.time >= trapReadyAt && LastKnownRoom.HasPlayerSide())
            {
                SealRoom(LastKnownRoom, trapDuration);
                trapReadyAt = Time.time + trapCooldown;
            }
        }

        IEnumerator AttackLifeSupport(ShipRoom room, bool freeze)
        {
            SealRoom(room, 0f);
            if (freeze) FreezeRoom(room);
            else VentRoom(room);
            yield return new WaitForSeconds(lifeSupportDuration);
            if (alert == AlertState.Lockdown || room == null) yield break;
            RestoreRoom(room);
            UnsealRoom(room);
        }

        void OnDoorwayEntered(ShipDoor door, GameObject character)
        {
            if (!crushDoors || controller != ShipFaction.Hostile || alert < AlertState.Alert || PlayerHas(ShipControl.Doors)) return;
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

        public void VentRoom(ShipRoom room)
        {
            room.VentOxygen();
            airChangedByShip.Add(room);
            Debug.Log("[ShipAI] Venting " + room.roomName);
        }

        public void FreezeRoom(ShipRoom room)
        {
            room.Freeze();
            airChangedByShip.Add(room);
            Debug.Log("[ShipAI] Freezing " + room.roomName);
        }

        public void RestoreRoom(ShipRoom room)
        {
            room.RestoreAtmosphere();
            airChangedByShip.Remove(room);
        }

        void RestoreAllAir()
        {
            foreach (var room in airChangedByShip)
            {
                if (room != null) room.RestoreAtmosphere();
            }
            airChangedByShip.Clear();
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

        //A hacked terminal hands one system to the player. The ship lets go of it straight away,
        //and once every terminal in the level is hacked the whole ship changes sides.
        public void GrantControl(ShipControl system)
        {
            if (!playerControls.Add(system)) return;
            switch (system)
            {
                case ShipControl.Doors:
                    ReleaseSeals();
                    break;
                case ShipControl.LifeSupport:
                    RestoreAllAir();
                    break;
                case ShipControl.Sensors:
                    //The ship goes blind: no new sightings, and what it knew fades
                    foreach (var sensor in FindObjectsOfType<ShipSensor>()) sensor.SetOnline(false);
                    break;
            }
            Debug.Log("[ShipAI] Player took " + system);
            ControlGranted?.Invoke(system);
            if (ShipTerminal.AllHacked) SetController(ShipFaction.Player);
        }

        // ---------- Player commands from the ship map; each needs its system hacked ----------

        public bool PlayerSeal(ShipRoom room, bool sealedShut)
        {
            if (room == null || !PlayerHas(ShipControl.Doors)) return false;
            room.SetDoorsSealed(sealedShut);
            foreach (var door in room.Doors) sealedByShip.Remove(door); //Now the player's seal, not the ship's
            return true;
        }

        //Slams the room's doors on any enemy standing in a doorway. Returns how many were hit.
        public int PlayerCrush(ShipRoom room)
        {
            if (room == null || !PlayerHas(ShipControl.Doors)) return 0;
            int hit = 0;
            foreach (var door in room.Doors)
            {
                if (door != null) hit += door.Crush(sparePlayerSide: true);
            }
            return hit;
        }

        public bool PlayerVent(ShipRoom room)
        {
            if (room == null || !PlayerHas(ShipControl.LifeSupport)) return false;
            room.VentOxygen();
            airChangedByShip.Remove(room);
            return true;
        }

        public bool PlayerFreeze(ShipRoom room)
        {
            if (room == null || !PlayerHas(ShipControl.LifeSupport)) return false;
            room.Freeze();
            airChangedByShip.Remove(room);
            return true;
        }

        public bool PlayerRestore(ShipRoom room)
        {
            if (room == null || !PlayerHas(ShipControl.LifeSupport)) return false;
            room.RestoreAtmosphere();
            airChangedByShip.Remove(room);
            return true;
        }

        //Called when every terminal is hacked (or directly, for testing) to hand the ship over
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
                RestoreAllAir();
            }
            Debug.Log("[ShipAI] Now controlled by " + controller);
            ControllerChanged?.Invoke(controller);
        }
    }
}
