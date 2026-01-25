using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;


public class Dead : IState
{
    private readonly AIController _aICon;
    public CombatTarget _combatTarget;
    public Mover _mover;
    public Fighter _fighter;

    public Dead(AIController aIController, CombatTarget combatTarget, Mover mover, Fighter fighter)
    {
        _aICon = aIController;
        _combatTarget = combatTarget;
        _mover = mover;
        _fighter = fighter;
    }

    public void OnEnter()
    {


    }

    public void OnExit()
    {

    }

    public void Tick()
    {
        //Handle Player Ai Death UI / Lives / Saving 
        //Handle Enemy Death
        //Dropper
        //LevellingUp
        //Scoreboard
    }
}