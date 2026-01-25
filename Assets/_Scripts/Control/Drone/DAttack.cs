using Rambler.Combat;
using Rambler.Control;
using Rambler.Movement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DAttack : IState
{
    DroneCon _droneCon;
    Mover _mover;
    DroneFOV _FOV;
    CombatTarget _otherCombatTarget;
    
    public DAttack(DroneCon droneCon, Mover mover, DroneFOV FOV, CombatTarget otherCombatTarget)
    {
        _droneCon = droneCon;
        _mover = mover;
        _FOV = FOV;
        _otherCombatTarget = otherCombatTarget;

    }
    public void OnEnter()
    {
        _droneCon.FacePlayer();
        _droneCon.IncreaseSpeed();
        _droneCon.TargetHealthCheck();
        _droneCon.AttackingTrue();
    }

    public void OnExit()
    {
       
    }

    public void Tick()
    {
       _droneCon.AttackTarget();
    }
}
