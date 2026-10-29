using System;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

namespace Rambler.Ship
{
    //One area of the ship, defined by its box collider (a trigger). Tracks who is inside and what
    //they are (humans, robots, drones), which doors lead out, its security camera, and the room's
    //air: oxygen and temperature, which the ship can drain or chill.
    [RequireComponent(typeof(BoxCollider))]
    public class ShipRoom : MonoBehaviour
    {
        static readonly List<ShipRoom> all = new();
        public static IReadOnlyList<ShipRoom> All => all;
        //Someone entered or left (sent with the character and true when they entered)
        public event Action<ShipRoom, GameObject, bool> OccupantsChanged;

        public string roomName;
        [SerializeField] List<ShipDoor> doors = new();

        [Header("Atmosphere")]
        [SerializeField] float normalOxygen = 100f;
        [SerializeField] float normalTemperature = 20f;
        [Tooltip("Coldest the room gets when frozen (C).")]
        [SerializeField] float freezeTemperature = -60f;
        [Tooltip("Oxygen % lost per second while venting.")]
        [SerializeField] float ventRate = 12f;
        [Tooltip("Oxygen % regained per second once restored.")]
        [SerializeField] float refillRate = 8f;
        [Tooltip("Degrees lost per second while freezing.")]
        [SerializeField] float coolRate = 8f;
        [Tooltip("Degrees regained per second once restored.")]
        [SerializeField] float warmRate = 5f;

        float oxygenTarget;
        float temperatureTarget;

        public float Oxygen { get; private set; }
        public float Temperature { get; private set; }
        public bool IsVenting => oxygenTarget < normalOxygen;
        public bool IsFreezing => temperatureTarget < normalTemperature;

        //A character can have several colliders, so count them and treat them as inside until all have left
        readonly Dictionary<GameObject, int> occupants = new();
        readonly List<GameObject> occupantList = new();
        BoxCollider box;

        public IReadOnlyList<ShipDoor> Doors => doors;

        public static ShipRoom At(Vector3 position)
        {
            foreach (var room in all)
            {
                if (room.Contains(position)) return room;
            }
            return null;
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            roomName = name;
        }

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            //Taps raycast against triggers; keep the room box out of the way of tap-to-move
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            if (string.IsNullOrEmpty(roomName)) roomName = name;
            Oxygen = oxygenTarget = normalOxygen;
            Temperature = temperatureTarget = normalTemperature;
        }

        void Update()
        {
            float o2Rate = oxygenTarget < Oxygen ? ventRate : refillRate;
            Oxygen = Mathf.MoveTowards(Oxygen, oxygenTarget, o2Rate * Time.deltaTime);
            float tempRate = temperatureTarget < Temperature ? coolRate : warmRate;
            Temperature = Mathf.MoveTowards(Temperature, temperatureTarget, tempRate * Time.deltaTime);
        }

        public void VentOxygen()
        {
            oxygenTarget = 0f;
        }

        public void Freeze()
        {
            temperatureTarget = freezeTemperature;
        }

        //Back to breathable, warm air; it recovers gradually rather than instantly
        public void RestoreAtmosphere()
        {
            oxygenTarget = normalOxygen;
            temperatureTarget = normalTemperature;
        }

        void OnEnable()
        {
            all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
        }

        public bool Contains(Vector3 position)
        {
            if (box == null) box = GetComponent<BoxCollider>(); //Editor tools call this outside Play mode
            //ClosestPoint returns the point itself when it is inside, and works for rotated boxes
            return (box.ClosestPoint(position) - position).sqrMagnitude < 0.0001f;
        }

        //Living characters currently in the room
        public IReadOnlyList<GameObject> Occupants
        {
            get
            {
                occupantList.Clear();
                foreach (var character in occupants.Keys)
                {
                    if (ShipCharacters.IsAlive(character)) occupantList.Add(character);
                }
                return occupantList;
            }
        }

        public bool HasPlayerSide()
        {
            foreach (var character in Occupants)
            {
                if (ShipCharacters.IsPlayerSide(character)) return true;
            }
            return false;
        }

        //Is anyone here who can suffocate or freeze (a Human), on the given side?
        public bool HasBreather(bool playerSide)
        {
            foreach (var character in Occupants)
            {
                if (ShipCharacters.IsPlayerSide(character) != playerSide) continue;
                var identity = character.GetComponent<CharacterIdentity>();
                if (identity != null && identity.BreathesAir) return true;
            }
            return false;
        }

        //How many living characters of a type are inside: either side, or just the player's / the enemy's
        public int Count(CharacterType type)
        {
            int count = 0;
            foreach (var character in Occupants)
            {
                if (TypeOf(character) == type) count++;
            }
            return count;
        }

        public int Count(CharacterType type, bool playerSide)
        {
            int count = 0;
            foreach (var character in Occupants)
            {
                if (TypeOf(character) == type && ShipCharacters.IsPlayerSide(character) == playerSide) count++;
            }
            return count;
        }

        public int Humans => Count(CharacterType.Human);
        public int Robots => Count(CharacterType.Robot);
        public int Drones => Count(CharacterType.Drone);

        //Characters without a CharacterIdentity count as nothing in particular
        static CharacterType? TypeOf(GameObject character)
        {
            var identity = character.GetComponent<CharacterIdentity>();
            return identity != null ? identity.Type : (CharacterType?)null;
        }

        //The camera watching this room (a working one if there is one), or null
        public ShipCamera Camera
        {
            get
            {
                ShipCamera broken = null;
                foreach (var cam in ShipCamera.All)
                {
                    if (!Contains(cam.transform.position)) continue;
                    if (!cam.IsDestroyed) return cam;
                    broken = cam;
                }
                return broken;
            }
        }

        public bool HasWorkingCamera
        {
            get
            {
                var cam = Camera;
                return cam != null && !cam.IsDestroyed;
            }
        }

        public void SetDoorsSealed(bool sealedShut)
        {
            foreach (var door in doors)
            {
                if (door != null) door.SetSealed(sealedShut);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null) return;
            occupants.TryGetValue(character, out int count);
            occupants[character] = count + 1;
            if (count == 0) OccupantsChanged?.Invoke(this, character, true);
        }

        void OnTriggerExit(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null || !occupants.TryGetValue(character, out int count)) return;
            if (count <= 1)
            {
                occupants.Remove(character);
                OccupantsChanged?.Invoke(this, character, false);
            }
            else occupants[character] = count - 1;
        }

        void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider>();
            if (col == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            //Blue normally, white as it freezes, grey-red as the air goes
            Color air = new Color(0.2f, 0.7f, 1f);
            if (Application.isPlaying)
            {
                air = Color.Lerp(new Color(0.6f, 0.2f, 0.2f), air, Oxygen / Mathf.Max(1f, normalOxygen));
                if (Temperature < 0f) air = Color.Lerp(air, Color.white, Mathf.Clamp01(-Temperature / 60f));
            }
            Gizmos.color = new Color(air.r, air.g, air.b, 0.08f);
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = new Color(air.r, air.g, air.b, 0.6f);
            Gizmos.DrawWireCube(col.center, col.size);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (var door in doors)
            {
                if (door != null) Gizmos.DrawLine(transform.position, door.transform.position);
            }

            //Every room needs a camera: link it in cyan, or cross the room out in red if there is none
            bool hasCamera = false;
            foreach (var cam in FindObjectsOfType<ShipCamera>())
            {
                if (!Contains(cam.transform.position)) continue;
                hasCamera = true;
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, cam.transform.position);
            }
            if (!hasCamera)
            {
                var bounds = GetComponent<BoxCollider>().bounds;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(bounds.min, bounds.max);
                Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z), new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
            }
        }
    }
}
