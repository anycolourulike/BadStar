using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class ICauseCaveIn : IState
{
    private readonly AIController _aIController;
    private readonly BoulderGennie _boulderGennie;
    private readonly Animator _animator;
    private readonly Mover _mover;    

    public ICauseCaveIn(AIController aIController, Mover mover, Animator animator, BoulderGennie boulderGennie)
    {
        _boulderGennie = boulderGennie;
        _aIController = aIController;        
        _animator = animator;
        _mover = mover;        
    }      

    public void OnEnter()
    {
        _animator.SetTrigger("jumpAttack");
    }

    public void OnExit()
    {
       
    }

    public void Tick()
    {
        
    }
}
