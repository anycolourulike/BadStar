using UnityEngine;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //A ship the Company can buy at a Shipyard. Keep these in a Resources folder so saves can find them;
    //every ship asset there is for sale.
    [CreateAssetMenu(fileName = "Ship", menuName = "Rambler/Company/Ship")]
    public class ShipDefinition : OwnableDefinition
    {
        [Tooltip("The ship model, shown turning in the Shipyard (only its meshes are used).")]
        [SerializeField] GameObject prefab = null;
        [Tooltip("Extra rotation that stands the model upright, for prefabs exported Z-up without the importer's 90° X.")]
        [SerializeField] Vector3 modelRotation = Vector3.zero;
        [SerializeField] ShipStats baseStats = new ShipStats { crewSeats = 2, range = 1, hull = 400 };
        [Header("Cargo runs")]
        [Tooltip("Credits a cargo run pays before part bonuses.")]
        [Min(0)] [SerializeField] int cargoRunPay = 300;
        [Tooltip("Real-time minutes a cargo run takes before part bonuses (keeps going while the game is closed).")]
        [Min(0.1f)] [SerializeField] float cargoRunMinutes = 20f;
        [Tooltip("Most of the hull a cargo run can cost, as a fraction.")]
        [Range(0f, 1f)] [SerializeField] float cargoRunWear = 0.1f;
        [Tooltip("Credits per hull point to repair.")]
        [Min(0)] [SerializeField] int repairCostPerPoint = 2;

        public GameObject Prefab => prefab;
        public Quaternion ModelRotation => Quaternion.Euler(modelRotation);
        public ShipStats BaseStats => baseStats;
        public int CargoRunPay => cargoRunPay;
        public float CargoRunMinutes => cargoRunMinutes;
        public float CargoRunWear => cargoRunWear;
        public int RepairCostPerPoint => repairCostPerPoint;
    }
}
