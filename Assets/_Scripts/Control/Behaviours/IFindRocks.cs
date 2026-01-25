using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class IFindRocks : IState
{
    private readonly AIController _aIController;
    private readonly BoulderGennie _boulderGennie;
    private readonly Mover _mover;

    public IFindRocks(AIController aIController, BoulderGennie boulderGennie, Mover mover)
    {
        _aIController = aIController;
        _boulderGennie = boulderGennie;
        _mover = mover;        
    }

    public void OnEnter()
    {
        
    }

    public void OnExit()
    {
        
    }

    public void Tick()
    {
        _boulderGennie.MoveToNearestRock();
    }     
}
