using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class ISitting : IState
{
    private readonly AIController _aIController;
    private readonly Animator _animator;

    public ISitting(AIController aIController, Animator animator)
    {
        _aIController = aIController;
        _animator = animator;
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
