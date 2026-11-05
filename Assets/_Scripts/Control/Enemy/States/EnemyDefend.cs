using UnityEngine;
using Rambler.Control;

//Holds cover and never advances. A guard holds near its post; a badly hurt enemy falls back to cover
//further from the target. Shoots whoever it can see in range, and grenades anyone who gets close.
public class EnemyDefend : IState
{
    readonly EnemyAI ai;
    float nextCoverCheck;

    public EnemyDefend(EnemyAI ai) => this.ai = ai;

    public void OnEnter()
    {
        ai.HoldFire();
        nextCoverCheck = 0f;
    }

    public void OnExit() => ai.HoldFire();

    public void Tick()
    {
        var cover = ai.Cover;
        bool settled = cover == null || cover.InCover || cover.IsMovingToCover;
        if (!settled && Time.time >= nextCoverCheck)
        {
            nextCoverCheck = Time.time + 3f;
            FindCover();
        }
        if (cover != null && cover.IsMovingToCover)
        {
            ai.HoldFire(); //Shooting would stop the agent short of cover
            if (ai.Agent.isStopped) ai.Agent.isStopped = false;
            return;
        }

        var target = ai.Target;
        if (target != null && ai.TargetVisible && ai.InRange(target)) ai.Shoot(target);
        else ai.HoldFire();

        //Too close for comfort: grenade them
        var thrower = ai.Thrower;
        if (thrower == null || target == null || !ai.TargetVisible) return;
        Vector3 at = target.transform.position;
        if (Vector3.Distance(ai.transform.position, at) > ai.DefendGrenadeRange) return;
        if (thrower.CanThrowAt(at) && ai.GrenadeTurnFree(target) && !ai.SquadmateNear(at, thrower.BlastRadius * 1.2f))
        {
            ai.HoldFire();
            thrower.Throw(at);
            ai.TakeGrenadeTurn(target, 5f);
        }
    }

    void FindCover()
    {
        var cover = ai.Cover;
        if (cover == null) return;
        Vector3 threat = ai.LastKnownPosition;
        Vector3 from;
        float radius;
        if (ai.IsGuard)
        {
            from = ai.Home;
            radius = ai.GuardRadius;
        }
        else
        {
            //Fall back: look for cover on the far side, away from the target
            Vector3 away = ai.transform.position - threat;
            away.y = 0f;
            from = ai.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : -ai.transform.forward) * 4f;
            radius = ai.CoverSearchRadius;
        }
        var point = cover.FindCover(threat, from, radius, ai.AttackRange * 0.95f, 2.5f, float.MaxValue);
        if (point != null) cover.MoveToCover(point, 1f);
        else if (ai.IsGuard && Vector3.Distance(ai.transform.position, ai.Home) > 1.5f) ai.MoveTo(ai.Home, 1f);
    }
}
