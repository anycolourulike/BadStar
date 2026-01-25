using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Core;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Movement;

public class TReload : IState
{

    private readonly TimbertoesCon _TCon;
    private readonly TFighter _fighter;

    public TReload(TimbertoesCon TCon, TFighter fighter)
    {
        _TCon = TCon;
        _fighter = fighter;
    }


    public void OnEnter()
    {
        _fighter.canAttack = false;
        _TCon.AttackingFalse();
        _TCon.ReloadingTrue();
        //_TCon.NotIdle();
        _fighter.ReloadSeq();
    }

    public void OnExit()
    {
        _fighter.canAttack = true;
    }

    public void Tick()
    {

    }
}

