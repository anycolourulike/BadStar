using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Mission
{
    //A visitor pass: guards don't treat this squad member as an intruder until the alarm goes up (SquadOrders.Alarm).
    public class Disguise : MonoBehaviour
    {
        static readonly HashSet<GameObject> worn = new HashSet<GameObject>();
        public static bool IsDisguised(GameObject who) => who != null && worn.Contains(who);

        void OnEnable() => worn.Add(gameObject);
        void OnDisable() => worn.Remove(gameObject);
    }
}
