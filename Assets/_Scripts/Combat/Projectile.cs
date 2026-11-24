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
    //Who fired it (set by ActiveWeapon). Null for projectiles from other sources (boulders, turrets).
    public GameObject Owner { get; set; }
    Rigidbody Rb;

    //Should this projectile hurt the character "victim"? Not its own shooter, and not the shooter's side:
    //enemies don't hurt enemies, and the player's side (Player, Companion) doesn't hurt itself.
    public bool CanHurt(GameObject victim)
    {
      if (Owner == null || victim == null) return true;
      if (victim == Owner || victim.transform.IsChildOf(Owner.transform)) return false;
      return !SameSide(Owner, victim);
    }

    static bool SameSide(GameObject a, GameObject b)
    {
      bool aEnemy = a.CompareTag("Enemy"), bEnemy = b.CompareTag("Enemy");
      if (aEnemy || bEnemy) return aEnemy && bEnemy;
      bool aPlayerSide = a.CompareTag("Player") || a.CompareTag("Companion");
      bool bPlayerSide = b.CompareTag("Player") || b.CompareTag("Companion");
      return aPlayerSide && bPlayerSide;
    }

    void Start()
    {
      Rb = GetComponent<Rigidbody>();
      transform.parent = null;

      if (isGrenade == true)
      {
          //Single throw impulse, then gravity takes over
          Vector3 throwDirection = transform.forward + transform.up * 5f;
          Rb.AddForce(throwDirection.normalized * 10f, ForceMode.Impulse);
          Invoke(nameof(ObjActiveFalse), 3f);
      }
      else
      {
          Invoke(nameof(ObjActiveFalse), maxLifeTime);
      }
    }

    void Update()
    {
        if (isGrenade == true) return;
        Rb.AddForce(transform.forward * Time.deltaTime * speed);
    }

    public void SetTarget(Vector3 target)
    {
      this.target = target;
      transform.LookAt(target);
    }

    //Set at launch from the shooter's upgrades (CombatModifiers); 1 = the prefab's damage
    public float DamageMultiplier { get; set; } = 1f;

    public float GetDamage()
    {
      return damage * DamageMultiplier;
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