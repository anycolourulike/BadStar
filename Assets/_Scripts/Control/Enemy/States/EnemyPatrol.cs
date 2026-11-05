using UnityEngine;
using Rambler.Control;

//Walks the patrol route, pausing at each waypoint
public class EnemyPatrol : IState
{
    readonly EnemyAI ai;
    int index;
    float dwell;

    public EnemyPatrol(EnemyAI ai) => this.ai = ai;

    public void OnEnter()
    {
        ai.HoldFire();
        dwell = 0f;
        //Pick up the route at the nearest waypoint
        var route = ai.Route;
        float best = float.MaxValue;
        for (int i = 0; i < route.transform.childCount; i++)
        {
            float d = Vector3.Distance(ai.transform.position, route.GetWaypoint(i));
            if (d < best) { best = d; index = i; }
        }
        ai.MoveTo(route.GetWaypoint(index), ai.PatrolSpeed);
    }

    public void OnExit() { }

    public void Tick()
    {
        var route = ai.Route;
        Vector3 waypoint = route.GetWaypoint(index);
        if (Vector3.Distance(ai.transform.position, waypoint) > ai.WaypointTolerance)
        {
            if (ai.Agent.isStopped || !ai.Agent.hasPath) ai.MoveTo(waypoint, ai.PatrolSpeed);
            return;
        }
        dwell += Time.deltaTime;
        if (dwell < ai.WaypointDwell) return;
        dwell = 0f;
        index = route.GetNextIndex(index);
        ai.MoveTo(route.GetWaypoint(index), ai.PatrolSpeed);
    }
}
