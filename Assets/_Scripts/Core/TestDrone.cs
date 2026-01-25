using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestDrone : MonoBehaviour
{
    public GameObject explosion;
    public Vector3 explosionOffset;
    public float destroyDelay;
    public float minForce;
    public float maxForce;
    public float radius;
    

    public void Start()
    {
        Explode();
    }

    public void Explode()
    {
        if (explosion != null)
        { 
            GameObject explosionFX = Instantiate(explosion, transform.position + explosionOffset, Quaternion.identity) as GameObject;
            Destroy(explosionFX, 5);
        }

        foreach (Transform t in transform)
        {
            var rb = t.GetComponent<Rigidbody>();

            if(rb != null)
            {
                rb.AddExplosionForce(Random.Range(minForce, maxForce), transform.position, radius);
            }

            Destroy(t.gameObject, destroyDelay);
        }
    }   

   
}
