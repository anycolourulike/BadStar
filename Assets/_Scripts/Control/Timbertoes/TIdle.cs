using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class TIdle : IState
{
    private readonly TimbertoesCon _TCon;
    private readonly TFighter _fighter;

    public TIdle(TimbertoesCon TCon, TFighter fighter)
    {
        _TCon = TCon;
        _fighter = fighter;
    }

    public void OnEnter()
    {
        //_TCon.IsIdle();
        _fighter.canAttack = false;
    }

    public void OnExit()
    {
        //_TCon.NotIdle();
        _fighter.canAttack = true;
        _TCon.AttackingTrue();
    }

    public void Tick()
    {
        //Play Idle Anim or Guard Anim
    }
}
