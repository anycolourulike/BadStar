using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Core;

public class Wall : MonoBehaviour
{
    Renderer wallRenderer;
    List<ParticleCollisionEvent> collisionEvents;

    void Start()  
    {
       wallRenderer = GetComponent<Renderer>();
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
         wallRenderer.material.SetVector("HitPos", hitPos);
    }
}
