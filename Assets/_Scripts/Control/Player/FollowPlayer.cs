using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class FollowPlayer : IState
{
    private readonly AIController _aIController;
    private readonly Mover _mover;

    public FollowPlayer(AIController aIController, Mover mover)
    {
        _aIController = aIController;
        _mover = mover;
    }

    public void OnEnter()
    {
        _mover.timerRunning = true;
    }

    public void OnExit()
    {
        
    }

    public void Tick()
    {
        
    }
    
}
