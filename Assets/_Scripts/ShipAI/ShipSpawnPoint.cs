using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //What the ship can send after the player
    public enum ShipUnit
    {
        Drone,         //Fast scouts, sent while the ship is suspicious
        Crew,          //Human NPCs, sent once the player is confirmed
        Reinforcement  //A larger wave that docks during Lockdown
    }

    //A place the ship can deploy units from: a drone hatch, crew quarters, a docking bay.
    //Place it on the NavMesh, inside the room it serves.
    public class ShipSpawnPoint : MonoBehaviour
    {
        static readonly List<ShipSpawnPoint> all = new();
        public static IReadOnlyList<ShipSpawnPoint> All => all;

        [SerializeField] ShipUnit[] serves = { ShipUnit.Drone, ShipUnit.Crew };
        [Tooltip("Optional effect played when a unit comes out (hatch opening, airlock steam...).")]
        [SerializeField] GameObject spawnFX;

        public ShipRoom Room { get; private set; }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Start()
        {
            Room = ShipRoom.At(transform.position + Vector3.up);
        }

        public bool Serves(ShipUnit unit)
        {
            foreach (var served in serves)
            {
                if (served == unit) return true;
            }
            return false;
        }

        //Humans are never sent out into air the ship has vented or frozen
        public bool IsSafeFor(ShipUnit unit)
        {
            if (unit == ShipUnit.Drone || Room == null) return true;
            return !Room.IsVenting && !Room.IsFreezing;
        }

        public GameObject Spawn(GameObject prefab, Transform parent)
        {
            //Spread units out a little so they don't stack on one spot
            var offset = Random.insideUnitCircle * 1.25f;
            var position = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (spawnFX != null)
            {
                spawnFX.SetActive(false);
                spawnFX.SetActive(true);
            }
            return Instantiate(prefab, position, transform.rotation, parent);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Serves(ShipUnit.Reinforcement) ? new Color(1f, 0.3f, 0.2f) : Serves(ShipUnit.Crew) ? new Color(1f, 0.8f, 0.2f) : new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.75f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
