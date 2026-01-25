using Rambler.Control;
using Rambler.Movement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DDead : IState
{

    public DroneCon _droneCon;
    public Mover _mover;
    public DDead(DroneCon droneCon, Mover mover)
    {
        _droneCon = droneCon;
        _mover = mover;
    }
    public void OnEnter()
    {
        //disable Mover
        //set bools
        //Explode
        //DestroyObject
    }

    public void OnExit()
    {
        throw new System.NotImplementedException();
    }

    public void Tick()
    {
        throw new System.NotImplementedException();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
