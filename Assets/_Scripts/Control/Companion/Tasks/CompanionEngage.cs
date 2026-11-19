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
    /// target; Success once it is gone. Stops shooting when it ends or is aborted.
    /// Without a gun it hides instead: takes the nearest cover near the player that faces the target, and holds fire.</summary>
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
        bool armed;

        const float MinThreatDistance = 6f; //Closest cover to the enemy it will take

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

            //Picking up or losing a gun mid-fight switches between shooting and hiding
            bool canShoot = brain.CanShoot;
            if (current != target || canShoot != armed)
            {
                target = current;
                armed = canShoot;
                if (armed) brain.Shoot(target);
                else brain.HoldFire();
                nextCoverTry = 0f; //New threat direction: look for cover against it
            }

            if (armed)
            {
                if (m_UseCover.Value && Time.time >= nextCoverTry) TryCover();
            }
            else if (Time.time >= nextCoverTry) TryHide();
            return TaskStatus.Running;
        }

        //Unarmed: any cover facing the threat, close to the player so the squad stays together
        void TryHide()
        {
            nextCoverTry = Time.time + Mathf.Max(0.5f, m_CoverRetry.Value);
            var cover = brain.Cover;
            if (cover == null || cover.InCover || cover.IsMovingToCover) return;
            Vector3 around = brain.Leader != null ? brain.Leader.transform.position : transform.position;
            CoverPoint point = cover.FindCover(target.transform.position, around,
                                               brain.CoverSearchRadius, float.MaxValue, MinThreatDistance, float.MaxValue);
            if (point != null) cover.MoveToCover(point, 1f);
        }

        void TryCover()
        {
            nextCoverTry = Time.time + Mathf.Max(0.5f, m_CoverRetry.Value);
            var cover = brain.Cover;
            if (cover == null || cover.InCover || cover.IsMovingToCover) return;
            //Not right under the enemy's nose: the nearest cover facing it can be a crate beside it
            CoverPoint point = cover.FindCover(target.transform.position, transform.position,
                                               brain.CoverSearchRadius, brain.AttackRange * 0.9f, Mathf.Min(MinThreatDistance, brain.AttackRange * 0.5f), float.MaxValue);
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
