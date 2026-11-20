using UnityEngine;
using Rambler.Combat;
using Rambler.Control;

//Fights from cover and works its way forward. The loop:
//take cover in range > shoot from it for a while > clear the target's position with a grenade >
//wait for the blast > push to cover closer to the target (or straight forward if there is none) > shoot again.
//Without grenades it pushes straight after shooting.
//With squadmates on the same target it works the flanks instead: one guard runs (firing on the move) to cover at a wide
//angle on the target while the others stay out of cover and give covering fire; then the next one goes.
public class EnemyAttack : IState
{
    readonly EnemyAI ai;
    enum Phase { Moving, Engage, Clear, Blast, Flank }
    const float FlankAngle = 55f;    //Degrees round the target from where it is now
    const float FlankSearch = 18f;   //Furthest cover it will run to
    const float FlankCooldown = 15f; //Seconds between flanks on the same target
    static readonly System.Collections.Generic.Dictionary<GameObject, float> nextFlank = new System.Collections.Generic.Dictionary<GameObject, float>();
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
        EndFlank();
        if (ai.Cover != null) ai.Cover.Suppressing = false;
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
            case Phase.Flank: Flank(target); break;
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

        //Cover to cover: a push with no cover closer in holds here and keeps fighting
        if (push) { StartEngage(); return; }
        //First contact with no cover in range: any cover facing the threat nearby, to fight from protection
        if (cover != null)
        {
            var anyCover = cover.FindCover(threat, ai.transform.position, ai.CoverSearchRadius * 1.5f, float.MaxValue, 2.5f, float.MaxValue);
            if (anyCover != null && cover.MoveToCover(anyCover, speed))
            {
                toCover = true;
                destination = anyCover.transform.position;
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
        //A squadmate is running to a flank: stay out and keep their target's head down
        bool suppressing = MateFlanking(target);
        if (ai.Cover != null) ai.Cover.Suppressing = suppressing;
        bool canShoot = target != null && ai.InRange(target) && (ai.TargetVisible || suppressing);
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
        if (ai.Cover != null) ai.Cover.Suppressing = false;
        if (TryFlank(target)) return;
        ClearThenPush();
    }

    // ---------- Flanking ----------

    bool MateFlanking(GameObject target)
    {
        foreach (var mate in EnemyAI.All)
            if (mate != ai && mate.Flanking && !mate.IsDead && mate.Target == target) return true;
        return false;
    }

    //Run to cover with a wide angle on the target while a squadmate on the same target covers it
    bool TryFlank(GameObject target)
    {
        if (target == null || ai.Cover == null || MateFlanking(target)) return false;
        if (!ai.Cover.InCover) return false; //Cover to cover: only breaks from cover
        if (nextFlank.TryGetValue(target, out float at) && Time.time < at) return false;
        bool covered = false;
        foreach (var mate in EnemyAI.All)
            if (mate != ai && !mate.IsDead && mate.Target == target && mate.StateName == "Attack") { covered = true; break; }
        if (!covered) return false;

        Vector3 t = target.transform.position;
        Vector3 fromNow = ai.transform.position - t; fromNow.y = 0f;
        float range = ai.AttackRange * 0.9f;
        CoverPoint best = null; float bestScore = float.MaxValue;
        foreach (var p in CoverPoint.All)
        {
            if (!p.IsFree) continue;
            Vector3 pos = p.transform.position;
            if (Mathf.Abs(pos.y - ai.transform.position.y) > 2f) continue;
            float run = Vector3.Distance(pos, ai.transform.position);
            if (run > FlankSearch || run < 3f) continue;
            Vector3 fromP = pos - t; fromP.y = 0f;
            float dist = fromP.magnitude;
            if (dist > range || dist < 4f) continue;
            if (Vector3.Dot(new Vector3(p.Facing.x, 0f, p.Facing.z).normalized, -fromP.normalized) < 0.35f) continue; //Its cover must face the target
            float angle = Vector3.Angle(fromNow, fromP);
            if (angle < FlankAngle) continue;
            float score = Mathf.Abs(angle - 90f) + run * 1.5f; //Near a right angle, not too far to run
            if (score < bestScore) { bestScore = score; best = p; }
        }
        if (best == null || !ai.Cover.MoveToCover(best, 1f)) return false;
        nextFlank[target] = Time.time + FlankCooldown;
        ai.Flanking = true;
        ai.Fighter.FireOnTheMove = true;
        ai.Shoot(target); //Run and gun
        destination = best.transform.position;
        toCover = true;
        StartPhase(Phase.Flank);
        return true;
    }

    void Flank(GameObject target)
    {
        if (target != null && ai.TargetVisible) ai.Shoot(target); else ai.HoldFire();
        bool there = ai.Cover.InCover;
        if (there || Time.time - phaseStarted > 9f || !ai.Cover.IsMovingToCover && !there)
        {
            EndFlank();
            StartEngage();
            return;
        }
        if (ai.Agent.isStopped) ai.Agent.isStopped = false; //A hit stops the agent; keep running
    }

    void EndFlank()
    {
        if (!ai.Flanking) return;
        ai.Flanking = false;
        if (ai.Fighter != null) ai.Fighter.FireOnTheMove = false;
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
