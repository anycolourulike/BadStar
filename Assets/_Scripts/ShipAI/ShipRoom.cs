using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //One area of the ship. A trigger box that tracks who is inside and which doors lead out.
    //Later phases add oxygen and temperature here.
    [RequireComponent(typeof(BoxCollider))]
    public class ShipRoom : MonoBehaviour
    {
        static readonly List<ShipRoom> all = new();
        public static IReadOnlyList<ShipRoom> All => all;

        public string roomName;
        [SerializeField] List<ShipDoor> doors = new();

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
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.08f);
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.6f);
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
