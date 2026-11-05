using UnityEngine;
using UnityEngine.AI;
using Rambler.Control;

//Goes to where the target was last seen or heard, then checks a few spots around it before giving up
public class EnemySearch : IState
{
    readonly EnemyAI ai;
    enum Step { GoToLead, Sweep, LookAround }
    Step step;
    Vector3 destination;
    float timer, lookTimer, lookYaw;
    int spotsChecked;

    public bool Finished { get; private set; }

    public EnemySearch(EnemyAI ai) => this.ai = ai;

    //A fresh lead while already searching starts the search over from it
    public void Restart()
    {
        Finished = false;
        timer = 0f;
        spotsChecked = 0;
        step = Step.GoToLead;
        destination = ai.LastKnownPosition;
        if (ai.isActiveAndEnabled && ai.StateName == "Search") ai.MoveTo(destination, 0.8f);
    }

    public void OnEnter()
    {
        ai.HoldFire();
        ai.LeadFollowed();
        Restart();
        ai.MoveTo(destination, 0.8f);
    }

    public void OnExit() => ai.LeadFollowed();

    public void Tick()
    {
        if (Finished) return;
        timer += Time.deltaTime;
        if (timer > ai.SearchTime) { GiveUp(); return; }

        switch (step)
        {
            case Step.GoToLead:
            case Step.Sweep:
                if (ai.Agent.isStopped) ai.MoveTo(destination, step == Step.GoToLead ? 0.8f : 0.45f);
                if (!ai.Arrived(0.6f)) return;
                step = Step.LookAround;
                lookTimer = 0f;
                lookYaw = ai.transform.eulerAngles.y;
                break;

            case Step.LookAround:
                //Turn one way then the other
                lookTimer += Time.deltaTime;
                float yaw = lookYaw + Mathf.Sin(lookTimer * 2.2f) * 70f;
                ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, Quaternion.Euler(0f, yaw, 0f), 180f * Time.deltaTime);
                if (lookTimer < 1.8f) return;
                if (spotsChecked >= ai.SearchSpots) { GiveUp(); return; }
                spotsChecked++;
                destination = RandomSpot(ai.LastKnownPosition, ai.SearchRadius);
                step = Step.Sweep;
                ai.MoveTo(destination, 0.45f);
                break;
        }
    }

    void GiveUp()
    {
        Finished = true;
        ai.Stop();
        ai.CalmDown();
    }

    static Vector3 RandomSpot(Vector3 around, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            var offset = Random.insideUnitCircle * radius;
            if (NavMesh.SamplePosition(around + new Vector3(offset.x, 0f, offset.y), out var hit, 2f, NavMesh.AllAreas)) return hit.position;
        }
        return around;
    }
}
