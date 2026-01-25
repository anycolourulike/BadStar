using System.Collections;
using System.Collections.Generic;
using Rambler.Movement;
using UnityEngine;
using Rambler.Core;
using Rambler.Attributes;
using Rambler.Control;

namespace Rambler.Combat 
{ 
    public class CombatTarget : MonoBehaviour
    {   
       float targetSpeed;
       Vector3 curTargetPos;
       Vector3 prevTargetPos; 
       Vector3 targetVelocity;            
         
       void Start() 
       {          
          prevTargetPos = transform.position;
       }  

       void Update() 
       {              
            curTargetPos = transform.position;
            Vector3 posDif = curTargetPos - prevTargetPos;
            targetVelocity = posDif/Time.deltaTime; 
            targetSpeed = targetVelocity.magnitude;
            prevTargetPos = curTargetPos;                              
       } 

       public Vector3 TargetFuturePos(Vector3 ShooterPos) 
       {
           var hitPointVector = GetHitPointVector(curTargetPos, targetVelocity, ShooterPos, 18.3f);
           return hitPointVector;
       }

        Vector3 GetHitPointVector(Vector3 targetPosition, Vector3 targetVelocity, Vector3 shooterPosition, float projectileSpeed)
        {
          Vector3 targetShooterVector = targetPosition - shooterPosition;
          float targetMoveAngle = Vector3.Angle(-targetShooterVector, targetVelocity) * Mathf.Deg2Rad;
          //if the target is stopping or if it is impossible for the projectile to catch up with the target (Sine Formula)
          
         if (targetVelocity.magnitude == 0 || targetVelocity.magnitude > projectileSpeed && Mathf.Sin(targetMoveAngle)
               / projectileSpeed > Mathf.Cos(targetMoveAngle) / targetVelocity.magnitude)
           {                         
             return targetPosition;
           }
         //also Sine Formula
          float shootAngle = Mathf.Asin(Mathf.Sin(targetMoveAngle) * targetSpeed / projectileSpeed);
          return targetPosition + targetVelocity * targetShooterVector.magnitude 
                / Mathf.Sin(Mathf.PI - targetMoveAngle - shootAngle) * Mathf.Sin(shootAngle)
                / targetVelocity.magnitude;
        }
  }
}
