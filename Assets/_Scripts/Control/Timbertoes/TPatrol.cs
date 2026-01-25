using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class TPatrol : IState
{
    private readonly TimbertoesCon _TCon;
    private readonly PatrolPath _patrolPath;
    private readonly Mover _mover;
    private readonly float _patrolSpeedFraction;
    public float _waypointDwellTime;
    public float _waypointTolerence;
    public int _currentWaypointIndex;
    public float _timeSinceArrivedAtWaypoint;
    public Vector3 _nextPosition;


    public TPatrol(TimbertoesCon TCon, PatrolPath patrolPath, Mover mover, float waypointTolerence, float waypointDwellTime, float patrolSpeedFraction, float timeSinceArrivedAtWaypoint, int currentWaypointIndex, Vector3 nextPosition)
    {
        _TCon = TCon;
        _patrolPath = patrolPath;
        _mover = mover;
        _waypointTolerence = waypointTolerence;
        _waypointDwellTime = waypointDwellTime;
        _patrolSpeedFraction = patrolSpeedFraction;
        _timeSinceArrivedAtWaypoint = timeSinceArrivedAtWaypoint;
        _currentWaypointIndex = currentWaypointIndex;
        _nextPosition = nextPosition;
    }

    void PatrolBehaviour()
    {
        if (_patrolPath != null)
        {
            if (AtWaypoint())
            {
                _timeSinceArrivedAtWaypoint = 0f;
                _TCon.AttackingTrue();
                _TCon.NotPatrolling();
                return;
                //CycleWaypoint();
            }
            _nextPosition = GetCurrentWaypoint();
        }
        if (_timeSinceArrivedAtWaypoint > _waypointDwellTime)
        {
            _mover.StartMoveAction(_nextPosition, _patrolSpeedFraction);
        }
    }

    private bool AtWaypoint()
    {
        float distanceToWaypoint = Vector3.Distance(_TCon.gameObject.transform.position, GetCurrentWaypoint());
        return distanceToWaypoint < _waypointTolerence;
    }

    private void CycleWaypoint()
    {
        _currentWaypointIndex = _patrolPath.GetNextIndex(_currentWaypointIndex);
    }

    public Vector3 GetCurrentWaypoint()
    {
        return _patrolPath.GetWaypoint(_currentWaypointIndex);
    }

    private void UpdateTimers()
    {
        _timeSinceArrivedAtWaypoint += Time.deltaTime;
    }

    public void OnEnter()
    {
        _TCon.IsPatrolling();
        _TCon.StopAttacking();
        CycleWaypoint();
       // _TCon.NotIdle();
    }

    public void OnExit()
    {
        _TCon.NotPatrolling();
    }

    public void Tick()
    {
        PatrolBehaviour();
        UpdateTimers();
    }
}
