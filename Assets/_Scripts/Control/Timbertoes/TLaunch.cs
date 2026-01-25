using Rambler.Control;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Movement;

public class TLaunch : IState
{
    TimbertoesCon _tCon;
    TFighter _fighter;
    Animator _anim;
    Mover _mover;

    public TLaunch(TimbertoesCon tCon, TFighter fighter, Animator anim, Mover mover)
    {
        _tCon = tCon;
        _fighter = fighter;
        _anim = anim;
        _mover = mover;
    }
    public void OnEnter()
    {
        _mover.CancelNav();
        _tCon.NotPatrolling();
        _fighter.RigWeightToZero();
        _anim.SetTrigger("Launch");
    }

    public void OnExit()
    {
        _fighter.RigWeightToOne();
        _tCon.IsPatrolling();        
    }

    public void Tick()
    {
        _mover.CancelNav();
    }
}
