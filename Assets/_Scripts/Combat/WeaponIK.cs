using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Combat
{
[System.Serializable]
public class HumanBone
{
    public HumanBodyBones bone;
    public float weight = 1.0f;
}
public class WeaponIK : MonoBehaviour
{
    public Transform targetTransform;
    //public Transform TargetTransform {get{return targetTransform;} set{targetTransform = value;}}
    
    public Transform aimTransform; 
    public Transform AimTransform {get{return aimTransform;} set{aimTransform = value;}}
    
    public int iterations = 10;
    [Range(0,1)]
    float weight = 1.0f;

    public float AimWeight {get{return weight;} set{weight = value;}}

    public float angleLimit = 90.0f;
    public float distanceLimit = 1.5f;

    public HumanBone[] humanBones;
    Transform [] boneTransforms;
  
    void Start()
    {
        Animator anim = GetComponent<Animator>();
        boneTransforms = new Transform[humanBones.Length];
        for(int i = 0; i < boneTransforms.Length; i++)
        {
            boneTransforms[i] = anim.GetBoneTransform(humanBones[i].bone);
        }
    }

    Vector3 GetTargetPosition() 
    {
        if(aimTransform != null)
        {
          Vector3 targetDirection = targetTransform.position - aimTransform.position;
          Vector3 aimDirection = aimTransform.forward;
          float blendOut = 0.0f;

         float targetAngle = Vector3.Angle(targetDirection, aimDirection);
        if(targetAngle > angleLimit)
        {
            blendOut += (targetAngle - angleLimit) / 50.0f;
        }

        float targetDistance = targetDirection.magnitude;
        if(targetDistance < distanceLimit)
        {
            blendOut += distanceLimit - targetDistance;
        }

         Vector3 direction = Vector3.Slerp(targetDirection, aimDirection, blendOut);
         return aimTransform.position + direction;
        }
        else
        {
            return new Vector3(0,0,0);
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {  
        if(aimTransform != null)
        {     
          Vector3 targetPosition = GetTargetPosition();
       
          for(int i = 0; i< iterations; i++)
          {
             for (int b = 0; b < boneTransforms.Length; b++)
             {
                Transform bone = boneTransforms[b];
                float boneWeight = humanBones[b].weight * weight;
                AimAtTarget(bone, targetPosition, boneWeight); 
             }
          }
        }               
    }

    private void AimAtTarget(Transform bone, Vector3 targetPosition, float weight)
    {
        Vector3 aimDirection = aimTransform.forward;
        Vector3 targetDirection = targetPosition - aimTransform.position;
        Quaternion aimTowards = Quaternion.FromToRotation(aimDirection, targetDirection);
        Quaternion blendedRotation = Quaternion.Slerp(Quaternion.identity, aimTowards, weight);
        bone.rotation = blendedRotation * bone.rotation;
        
    }
  }
}
