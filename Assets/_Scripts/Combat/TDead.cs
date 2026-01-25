using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;
using UnityEngine.Animations.Rigging;

public class TDead : IState
{
    private readonly TimbertoesCon _aICon;
    public CombatTarget _combatTarget;
    public Mover _mover;
    public TFighter _fighter;

    public TDead(TimbertoesCon aIController, CombatTarget combatTarget, Mover mover, TFighter fighter)
    {
        _aICon = aIController;
        _combatTarget = combatTarget;
        _mover = mover;
        _fighter = fighter;
    }

    public void OnEnter()
    {
        _combatTarget.enabled = false;
        _mover.enabled = false;
        _fighter.enabled = false;
        _aICon.NotPatrolling();
        _aICon.AttackingFalse();


    }

    public void OnExit()
    {

    }

    public void Tick()
    {

    }
}
