using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;

namespace Rambler.Control.CompanionTasks
{
    /// <summary>Success while the companion has an enemy worth fighting: seen in the last moment, alive,
    /// near the player, and the player is still within the leash. Put it first in a Sequence with
    /// Abort Type Both so the fight starts and ends as this changes.</summary>
    [Category("BadStar")]
    [Description("Success while the CompanionBrain has an enemy to fight and the player is within the leash.")]
    public class CompanionSeesEnemy : Conditional
    {
        CompanionBrain brain;

        /// <summary>Caches the brain.</summary>
        public override void OnAwake()
        {
            brain = gameObject.GetComponent<CompanionBrain>();
        }

        /// <summary>Reports whether there is an enemy to fight.</summary>
        public override TaskStatus OnUpdate()
        {
            return brain != null && brain.enabled && brain.HasEnemy ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
