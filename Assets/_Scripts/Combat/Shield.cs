using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Core;


namespace Rambler.Combat
{   
  public class Shield : MonoBehaviour
  {    

    Renderer shieldRenderer;
    List<ParticleCollisionEvent> collisionEvents;

    void Start()  
    {
       shieldRenderer = GetComponent<Renderer>();
       collisionEvents = new List<ParticleCollisionEvent>();
    }  
    void OnParticleCollision(GameObject particleProj) 
    {
      var proj = particleProj.GetComponent<Projectile>();
      var part = proj.GetComponent<ParticleSystem>();
      int numCollisionEvents = part.GetCollisionEvents(this.gameObject, collisionEvents);

      int i = 0; 

      if (i < numCollisionEvents)
      {
       Vector3 pos = collisionEvents[i].intersection;
      
       
       //var pool = proj.GetComponent<GameObject>();
       Instantiate(proj.HitEffect(), pos, this.transform.rotation);
       Destroy(particleProj);
       //pool.SetActive(false);   
      }       
     
    }  

    void HitShield(Vector3 hitPos) 
    {
         shieldRenderer.material.SetVector("HitPos", hitPos);
    }
  }   
} 

