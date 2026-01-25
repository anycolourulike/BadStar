using Rambler.Control;
using Rambler.Movement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DPatrol : IState
{
    private readonly DroneCon _droneCon;
    private readonly PatrolPath _patrolPath;
    private readonly Mover _mover;
    private readonly float _patrolSpeedFraction;
    public float _waypointDwellTime;
    public float _waypointTolerence;
    public int _currentWaypointIndex;
    public float _timeSinceArrivedAtWaypoint;
    public Vector3 _nextPosition;


    public DPatrol(DroneCon droneController, PatrolPath patrolPath, Mover mover, float waypointTolerence, float waypointDwellTime, float patrolSpeedFraction, float timeSinceArrivedAtWaypoint, int currentWaypointIndex, Vector3 nextPosition)
    {
        _droneCon = droneController;
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
                CycleWaypoint();
            }
            _nextPosition = GetCurrentWaypoint();
        }
        if (_timeSinceArrivedAtWaypoint > _waypointDwellTime)
        {
            _droneCon.FaceWaypoint(_nextPosition);
            _mover.StartMoveAction(_nextPosition, _patrolSpeedFraction);
        }
    }

    private bool AtWaypoint()
    {
        float distanceToWaypoint = Vector3.Distance(_droneCon.gameObject.transform.position, GetCurrentWaypoint());
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
        _droneCon.IsPatrolling();
        _droneCon.AttackingFalse();
    }

    public void OnExit()
    {
       _droneCon.NotPatrolling();
    }

    public void Tick()
    {
        PatrolBehaviour();
        UpdateTimers();
    }
}
