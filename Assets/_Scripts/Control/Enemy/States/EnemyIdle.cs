using UnityEngine;
using Rambler.Control;

//Stands at its post, walking back to it first if it was drawn away. Rested once it has stood there a while.
public class EnemyIdle : IState
{
    readonly EnemyAI ai;
    float restTimer;
    bool goingHome;

    public bool Rested => !goingHome && restTimer >= ai.IdleTime;

    public EnemyIdle(EnemyAI ai) => this.ai = ai;

    public void OnEnter()
    {
        restTimer = 0f;
        ai.HoldFire();
        goingHome = Vector3.Distance(ai.transform.position, ai.Home) > 1.5f;
        if (goingHome) ai.MoveTo(ai.Home, ai.PatrolSpeed);
        else ai.Stop();
    }

    public void OnExit() { }

    public void Tick()
    {
        if (goingHome)
        {
            if (ai.Agent.isStopped) ai.MoveTo(ai.Home, ai.PatrolSpeed); //A hit stops the agent
            if (!ai.Arrived()) return;
            goingHome = false;
            ai.Stop();
        }
        ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, ai.HomeRotation, 120f * Time.deltaTime);
        restTimer += Time.deltaTime;
    }
}
