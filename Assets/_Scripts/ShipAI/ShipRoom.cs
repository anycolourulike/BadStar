using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //One area of the ship. A trigger box that tracks who is inside, which doors lead out,
    //and the room's air: oxygen and temperature, which the ship can drain or chill.
    [RequireComponent(typeof(BoxCollider))]
    public class ShipRoom : MonoBehaviour
    {
        static readonly List<ShipRoom> all = new();
        public static IReadOnlyList<ShipRoom> All => all;

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
        }

        void OnTriggerExit(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null || !occupants.TryGetValue(character, out int count)) return;
            if (count <= 1) occupants.Remove(character);
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
        }
    }
}
