using System;
using UnityEngine;
using UnityEngine.AI;

namespace Rambler.Mission
{
    //The person the mission is about (the scientist now, the Chair later). Killed by the drone strike or a
    //silenced shot; plays a death and stops moving. MissionScript listens to Killed.
    public class MissionTarget : MonoBehaviour
    {
        [SerializeField] string displayName = "Target";
        [SerializeField] string[] deathTriggers = { "Die2", "Die3" };

        public bool IsDead { get; private set; }
        public string DisplayName => displayName;
        public static event Action<MissionTarget> Killed;

        //Animation event on the death clips (Health handles it on other characters)
        public void HitTheFloor() { }

        public void Kill()
        {
            if (IsDead) return;
            IsDead = true;
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            var mover = GetComponent<Rambler.Movement.Mover>();
            if (mover != null) mover.enabled = false;
            var anim = GetComponent<Animator>();
            if (anim != null && deathTriggers.Length > 0) anim.SetTrigger(deathTriggers[UnityEngine.Random.Range(0, deathTriggers.Length)]);
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            Debug.Log("[Mission] " + displayName + " killed");
            Killed?.Invoke(this);
        }
    }
}
