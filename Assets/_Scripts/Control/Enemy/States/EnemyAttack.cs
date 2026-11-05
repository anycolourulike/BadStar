using UnityEngine;
using Rambler.Combat;
using Rambler.Control;

//Fights from cover and works its way forward. The loop:
//take cover in range > shoot from it for a while > clear the target's position with a grenade >
//wait for the blast > push to cover closer to the target (or straight forward if there is none) > shoot again.
//Without grenades it pushes straight after shooting.
public class EnemyAttack : IState
{
    readonly EnemyAI ai;
    enum Phase { Moving, Engage, Clear, Blast }
    Phase phase;
    Vector3 destination;
    float speed;
    float phaseStarted;
    float engageUntil;
    float blindTimer;
    float blastAt;
    bool toCover;

    public EnemyAttack(EnemyAI ai) => this.ai = ai;

    public void OnEnter()
    {
        ai.HoldFire();
        TakePosition(push: false);
    }

    public void OnExit()
    {
        ai.HoldFire();
    }

    public void Tick()
    {
        var target = ai.Target;
        switch (phase)
        {
            case Phase.Moving: Moving(target); break;
            case Phase.Engage: Engage(target); break;
            case Phase.Clear: Clear(); break;
            case Phase.Blast: Blast(); break;
        }
    }

    //Cover in range of the target. When pushing, it has to be a real step closer.
    void TakePosition(bool push)
    {
        Vector3 threat = ai.LastKnownPosition;
        float dist = Vector3.Distance(ai.transform.position, threat);
        float range = ai.AttackRange * 0.9f;
        var cover = ai.Cover;
        speed = push ? 1f : 0.9f;

        if (cover != null)
        {
            var point = cover.FindCover(threat, ai.transform.position, ai.CoverSearchRadius, range, 2.5f, push ? dist - ai.PushStep : float.MaxValue);
            if (point != null && cover.MoveToCover(point, speed))
            {
                toCover = true;
                destination = point.transform.position;
                StartPhase(Phase.Moving);
                return;
            }
        }

        toCover = false;
        Vector3 dir = threat - ai.transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : ai.transform.forward;
        if (push)
        {
            //No cover closer in: walk straight up, but not right on top of them
            float step = Mathf.Min(ai.PushStep * 1.5f, dist - ai.ClosestPush);
            if (step < 0.5f) { StartEngage(); return; }
            destination = ai.transform.position + dir * step;
        }
        else
        {
            if (dist <= range && ai.TargetVisible) { ai.Stop(); StartEngage(); return; }
            destination = threat - dir * Mathf.Min(range * 0.7f, dist);
        }
        ai.MoveTo(destination, speed);
        StartPhase(Phase.Moving);
    }

    void Moving(GameObject target)
    {
        ai.HoldFire(); //Shooting would stop the agent
        float elapsed = Time.time - phaseStarted;
        bool close = target != null && ai.TargetVisible && Vector3.Distance(ai.transform.position, target.transform.position) < 4f;
        bool there = toCover ? ai.Cover.InCover : ai.Arrived(0.5f);
        if (there || close || elapsed > 8f)
        {
            if (toCover && !there) ai.Cover.GiveUpHeading(); //Fight from here instead
            StartEngage();
            return;
        }
        if (ai.Agent.isStopped)
        {
            //A hit stops the agent; carry on to cover, or to the spot
            if (toCover) ai.Agent.isStopped = false;
            else ai.MoveTo(destination, speed);
        }
    }

    void StartEngage()
    {
        engageUntil = Time.time + Random.Range(ai.EngageTime.x, ai.EngageTime.y);
        blindTimer = 0f;
        StartPhase(Phase.Engage);
    }

    void Engage(GameObject target)
    {
        bool canShoot = target != null && ai.TargetVisible && ai.InRange(target);
        if (canShoot)
        {
            ai.Shoot(target);
            blindTimer = 0f;
        }
        else
        {
            ai.HoldFire();
            blindTimer += Time.deltaTime;
        }

        bool outOfRange = target != null && ai.TargetVisible && !ai.InRange(target);
        bool cantSeeFromHere = blindTimer > 2.5f;
        bool shotEnough = Time.time >= engageUntil;
        if (!outOfRange && !cantSeeFromHere && !shotEnough) return;

        float dist = Vector3.Distance(ai.transform.position, ai.LastKnownPosition);
        if (shotEnough && !outOfRange && !cantSeeFromHere && dist <= ai.ClosestPush)
        {
            engageUntil = Time.time + Random.Range(ai.EngageTime.x, ai.EngageTime.y); //Already close: keep fighting here
            return;
        }
        ClearThenPush();
    }

    //Grenade the target's position first if it can, otherwise push straight away
    void ClearThenPush()
    {
        var thrower = ai.Thrower;
        Vector3 at = ai.LastKnownPosition;
        if (thrower != null && thrower.CanThrowAt(at) && ai.GrenadeTurnFree(ai.Target) && !ai.SquadmateNear(at, thrower.BlastRadius * 1.2f))
        {
            ai.HoldFire();
            ai.Stop();
            thrower.Throw(at);
            ai.TakeGrenadeTurn(ai.Target, 5f);
            StartPhase(Phase.Clear);
            return;
        }
        TakePosition(push: true);
    }

    //Wait for the grenade to leave the hand
    void Clear()
    {
        if (ai.Thrower.IsThrowing && Time.time - phaseStarted < 4f) return;
        blastAt = Time.time + ai.Thrower.Fuse + ai.PushAfterBlast;
        StartPhase(Phase.Blast);
    }

    //Keep its head down until it goes off, then move up
    void Blast()
    {
        ai.HoldFire();
        if (Time.time < blastAt) return;
        TakePosition(push: true);
    }

    void StartPhase(Phase next)
    {
        phase = next;
        phaseStarted = Time.time;
    }
}
