using UnityEngine;

namespace Rambler.Crew.Battle
{
    //The tactical map a ship battle is fought on: a layer far below the ship interior with the Titan contour surface,
    //a light for each ship, and (off to one side) a model of the enemy ship that the Weapons screen photographs from
    //above for its target picture. Built into the ShipBattle scene by Tools > Company > Build Tactical Space.
    public class TacticalSpace : MonoBehaviour
    {
        public static TacticalSpace Instance { get; private set; }

        public TacticalShip playerShip;
        public TacticalShip enemyShip;
        [Tooltip("The enemy ship's model, photographed from above for the target picture.")]
        public Transform portrait;
        [Tooltip("Missile and rocket prefabs (TacticalMissile).")]
        public TacticalMissile missilePrefab;
        public TacticalMissile rocketPrefab;
        public TacticalMissile enemyRocketPrefab;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
