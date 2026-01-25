using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class ThrowGrenade : IState
{
    private readonly AIController _aIController;
    private readonly Fighter _fighter;

    public ThrowGrenade(AIController aIController, Fighter fighter)
    {
        _aIController = aIController;
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
       
    }
}
