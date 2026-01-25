using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

namespace Rambler.Combat
{
    [CreateAssetMenu(fileName = "Drone", menuName = "drone/new", order = 0)]
    public class Drone : ScriptableObject
    {
        public GameObject droneObj;
        public DroneCon droneCon;

        public enum DroneType { suicide, ranged, health }
        public DroneType droneType;       


        void Start()
        {
            //droneType = (DroneType)Random.Range(0, System.Enum.GetValues(typeof(DroneType)).Length);
            droneCon = droneObj.GetComponentInChildren<DroneCon>();
            switch (droneType)
            {
                case DroneType.suicide:
                    droneCon.isSuicide = true;
                    break;
                case DroneType.ranged:
                    droneCon.isRanged = true;
                    break;
                case DroneType.health:
                    droneCon.isHealth = true;
                    break;
            }            
        }
      
    }
}
