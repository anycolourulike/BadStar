using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.Shared.Utility;
using Action = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Action;

namespace Rambler.Control.CompanionTasks
{
    /// <summary>Keeps the companion in its squad slot behind the character the player controls:
    /// first companion back-left, second back-right, third straight behind. Runs to catch up when far
    /// behind, walks when close, and stops once in place and the player has stopped. Always Running.</summary>
    [Category("BadStar")]
    [Description("Follows the player in a loose formation. Always Running; put it last in the Selector.")]
    public class FollowLeader : Action
    {
        [Tooltip("How far behind the player the slots are.")]
        [SerializeField] SharedVariable<float> m_Behind = 1.8f;
        [Tooltip("How far to the side of the player the first two slots are.")]
        [SerializeField] SharedVariable<float> m_Side = 1.6f;
        [Tooltip("Counts as in place within this distance of its slot.")]
        [SerializeField] SharedVariable<float> m_Tolerance = 1.2f;
        [Tooltip("Runs flat out when further than this from its slot; walks when closer.")]
        [SerializeField] SharedVariable<float> m_RunDistance = 5f;
        [Range(0f, 1f)]
        [Tooltip("Walking speed as a share of full speed.")]
        [SerializeField] SharedVariable<float> m_WalkSpeed = 0.55f;

        CompanionBrain brain;
        Vector3 lastDestination;
        bool moving;

        /// <summary>Caches the brain.</summary>
        public override void OnAwake()
        {
            brain = gameObject.GetComponent<CompanionBrain>();
        }

        /// <summary>Starts with a fresh path.</summary>
        public override void OnStart()
        {
            moving = false;
            lastDestination = new Vector3(float.MaxValue, 0f, 0f);
        }

        /// <summary>Moves toward its slot, or waits in it.</summary>
        public override TaskStatus OnUpdate()
        {
            if (brain == null) return TaskStatus.Failure;
            var leader = brain.Leader;
            //Holding where the player left it (a mission's plan), or nobody to follow
            if (leader == null || brain.IsControlled || SquadOrders.HoldPosition)
            {
                Halt();
                return TaskStatus.Running;
            }

            Vector3 slot = Slot(leader.transform);
            float fromSlot = Flat(slot - transform.position).magnitude;
            var leaderAgent = leader.GetComponent<NavMeshAgent>();
            bool leaderMoving = leaderAgent != null && leaderAgent.velocity.sqrMagnitude > 0.04f;

            if (fromSlot <= m_Tolerance.Value && !leaderMoving)
            {
                Halt();
                return TaskStatus.Running;
            }

            //Pace: a walk near the slot, the leader's own speed plus a catch-up share of the gap when it moves,
            //flat out only when well out of place (in-between paces made the feet skid)
            float max = Mathf.Max(0.1f, brain.MaxSpeed);
            float leaderSpeed = leaderAgent != null ? leaderAgent.velocity.magnitude : 0f;
            float run = brain.RunPace;
            float pace = fromSlot > m_RunDistance.Value ? run
                       : Mathf.Min(run, Mathf.Max(brain.WalkPace, leaderMoving ? leaderSpeed * 1.05f + Mathf.Max(0f, fromSlot - m_Tolerance.Value) * 0.8f : 0f));
            float speed = Mathf.Clamp01(pace / max);
            brain.SetSpeed(speed);
            //Only repath when the slot has moved a little, so the agent isn't re-planning every frame
            if (!moving || (slot - lastDestination).sqrMagnitude > 0.25f)
            {
                brain.MoveTo(slot, speed);
                lastDestination = slot;
                moving = true;
            }
            return TaskStatus.Running;
        }

        Vector3 Slot(Transform leader)
        {
            Vector3 back = -Flat(leader.forward).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -back);
            Vector3 offset = brain.SquadSlot switch
            {
                0 => back * m_Behind.Value - right * m_Side.Value,
                1 => back * m_Behind.Value + right * m_Side.Value,
                _ => back * (m_Behind.Value * 1.8f),
            };
            Vector3 slot = leader.position + offset;
            //Behind the player is a wall: stand on the nearest walkable spot instead
            if (NavMesh.SamplePosition(slot, out var hit, 2f, NavMesh.AllAreas)) slot = hit.position;
            else slot = leader.position;
            return slot;
        }

        void Halt()
        {
            if (moving) brain.Stop();
            moving = false;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v : Vector3.forward;
        }

        /// <summary>Stops on the spot when the branch is interrupted (e.g. to fight).</summary>
        public override void OnEnd()
        {
            if (brain != null) Halt();
        }
    }
}
