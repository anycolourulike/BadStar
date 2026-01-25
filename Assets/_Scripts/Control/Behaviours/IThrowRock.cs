using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;
using System;

public class IThrowRock : IState
{
    BoulderGennie _boulderGennie;

    public IThrowRock(AIController aiController, BoulderGennie boulderGennie)
    {
        _boulderGennie = boulderGennie;
    }

    public void OnEnter()
    {
      _boulderGennie.ThrowRocks();
    }

    public void OnExit()
    {
        
    }

    public void Tick()
    {
        
    }
    
}
