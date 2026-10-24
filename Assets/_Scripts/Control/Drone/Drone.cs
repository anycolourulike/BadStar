using UnityEngine;
using Rambler.Control;

namespace Rambler.Combat
{
    [CreateAssetMenu(fileName = "Drone", menuName = "drone/new", order = 0)]
    public class Drone : ScriptableObject
    {
        public enum DroneType { suicide, ranged, health }

        public GameObject droneObj;
        public DroneType droneType;

        //ScriptableObjects never get Start(), so the type is applied to each spawned drone here
        public GameObject Spawn(Vector3 position)
        {
            if (droneObj == null)
            {
                Debug.LogWarning(name + " has no drone prefab assigned");
                return null;
            }

            GameObject instance = Instantiate(droneObj, position, Quaternion.identity);

            DroneCon droneCon = instance.GetComponentInChildren<DroneCon>();
            if (droneCon != null)
            {
                droneCon.isSuicide = droneType == DroneType.suicide;
                droneCon.isRanged = droneType == DroneType.ranged;
                droneCon.isHealth = droneType == DroneType.health;
            }

            return instance;
        }
    }
}
