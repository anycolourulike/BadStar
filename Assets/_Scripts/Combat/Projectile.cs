using Rambler.Core;
using Rambler.Movement;
using Rambler.Control;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Rambler.Combat
{
  public class Projectile : MonoBehaviour
  {    
    [SerializeField] GameObject hitEffect = null;   
    [SerializeField] float maxLifeTime = 0.8f; 
    [SerializeField] float speed = 5f; 
    [SerializeField] float damage;     
    public Vector3 target;
    public bool isGrenade;
    Rigidbody Rb;  

    void Start()
    {
      Rb = GetComponent<Rigidbody>();
      transform.parent = null;      
    }

    void Update() 
    {
        if (isGrenade == true)
        {
            Vector3 throwDirection = transform.forward + transform.up * 5f;
            Rb.AddForce(throwDirection.normalized * 10f, ForceMode.Impulse);
            Invoke("ObjActiveFalse", 3f); ;
        }
        else
        {
            Rb.AddForce(transform.forward * Time.deltaTime * speed);
            Invoke("ObjActiveFalse", maxLifeTime);
        }
    }  

    public void SetTarget(Vector3 target)
    {
      this.target = target;
      transform.LookAt(target);
    }

    public float GetDamage() 
    {
      return damage;
    }

    public Vector3 GetAimLocation()
    {        
      return target;                      
    }        
    
    public GameObject HitEffect()
    {
      return hitEffect;
    } 

    void ObjActiveFalse() 
    {      
      //MF_AutoPool.Despawn(gameObject);
      Destroy(gameObject);
    }
  }
}