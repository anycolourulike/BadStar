using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;

public class TAttack : IState
{

    private readonly TimbertoesCon _TCon;
    public GameObject _closestTarget;
    private readonly Mover _mover;
    public CombatTarget _otherCombatTarget;
    private readonly TimbertoesFOV _FOV;
    public float _TimerForNextAttack;
    public float _timeSinceLastSawPlayer;
    private float _turnTime = 3f;
    private readonly float _coolDown;
    public CapsuleCollider _capsuleCol;
    public TFighter _fighter;

    public TAttack(TimbertoesCon TCon, TFighter fighter, Mover mover, TimbertoesFOV FOV, float TimerForNextAttack, float timeSinceLastSawPlayer, float turnTime, float coolDown)
    {
        _TCon = TCon;
        _FOV = FOV;
        _TimerForNextAttack = TimerForNextAttack;
        _timeSinceLastSawPlayer = timeSinceLastSawPlayer;
        _turnTime = turnTime;
        _coolDown = coolDown;
        _fighter = fighter;
        _mover = mover;
    }

    void AttackTimer()
    {
       if (_turnTime >= 0f)
       {
            if (_TimerForNextAttack > 0)
            {
                _TimerForNextAttack -= Time.deltaTime;
            }
            else if (_TimerForNextAttack <= 0)
            {
                if (_capsuleCol != null)
                {
                    _TCon.AttackBehaviour();
                    _TimerForNextAttack = _coolDown;
                }
            }
       }
       else
       {
            _TCon.StopAttacking();
       }
    }

    private void UpdateTimers()
    {
        _timeSinceLastSawPlayer += Time.deltaTime;
        _turnTime -= Time.deltaTime;
    }

    public void OnEnter()
    {
        _fighter.canAttack = true;
        _turnTime = 3f;
        _TCon.NotPatrolling();
        //_TCon.NotIdle();
        _TCon.TargetHealthCheck();
        _TCon.AttackingTrue();
        _timeSinceLastSawPlayer = 2f;
        _mover.CancelNav();
    }

    public void OnExit()
    {
        _turnTime = 0f;
        _fighter.canAttack = false;
        _TCon.AttackingFalse();
        _TCon.IsPatrolling();
        _fighter.TargetCap = null;
        _fighter.CombatTarget = null;
        _fighter.EnemyPos = null;
    }

    public void Tick()
    {
        AttackTimer();
        UpdateTimers();
        _mover.CancelNav();
    }
}

