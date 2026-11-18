using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.Shared.Utility;
using Rambler.Combat;
using Action = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Action;

namespace Rambler.Control.CompanionTasks
{
    /// <summary>Fights the CompanionBrain's target: takes free cover facing it (within weapon range) when
    /// there is some, and shoots. Switches when the brain picks a new target. Running while there is a
    /// target; Success once it is gone. Stops shooting when it ends or is aborted.</summary>
    [Category("BadStar")]
    [Description("Takes cover facing the companion's target and shoots it. Pair with Companion Sees Enemy.")]
    public class CompanionEngage : Action
    {
        [Tooltip("Look for cover at all (off: stand and shoot).")]
        [SerializeField] SharedVariable<bool> m_UseCover = true;
        [Tooltip("Seconds between looking for cover while out of it.")]
        [SerializeField] SharedVariable<float> m_CoverRetry = 3f;

        CompanionBrain brain;
        GameObject target;
        float nextCoverTry;

        /// <summary>Caches the brain.</summary>
        public override void OnAwake()
        {
            brain = gameObject.GetComponent<CompanionBrain>();
        }

        /// <summary>Starts a fresh fight.</summary>
        public override void OnStart()
        {
            target = null;
            nextCoverTry = 0f;
        }

        /// <summary>Keeps shooting the current target and keeps trying for cover.</summary>
        public override TaskStatus OnUpdate()
        {
            if (brain == null) return TaskStatus.Failure;
            var current = brain.Target;
            if (current == null) return TaskStatus.Success;

            if (current != target)
            {
                target = current;
                brain.Shoot(target);
                nextCoverTry = 0f; //New threat direction: look for cover against it
            }

            if (m_UseCover.Value && Time.time >= nextCoverTry) TryCover();
            return TaskStatus.Running;
        }

        void TryCover()
        {
            nextCoverTry = Time.time + Mathf.Max(0.5f, m_CoverRetry.Value);
            var cover = brain.Cover;
            if (cover == null || cover.InCover || cover.IsMovingToCover) return;
            CoverPoint point = cover.FindCover(target.transform.position, transform.position,
                                               brain.CoverSearchRadius, brain.AttackRange * 0.9f, 0f, float.MaxValue);
            if (point != null) cover.MoveToCover(point, 1f);
        }

        /// <summary>Stops shooting and gives up any cover it was running to.</summary>
        public override void OnEnd()
        {
            if (brain != null)
            {
                brain.HoldFire();
                if (brain.Cover != null) brain.Cover.GiveUpHeading();
            }
            target = null;
        }
    }
}
