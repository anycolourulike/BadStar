using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Rambler.Control;
using Rambler.Core;

public class Boulder : MonoBehaviour
{

    [SerializeField] GameObject landFx;
    public Rigidbody rb;
    bool FXStarted;

    void Start()
    {
        rb = GetComponent<Rigidbody>();          
    }

    //Find land point and instantiate landFX
    public void OnTriggerEnter(Collider other)
    {
        Vector3 pointOfIntersection = other.ClosestPoint(transform.position);

        if (other.CompareTag("plane"))
        {
            InstantiateLandFX(pointOfIntersection);
            AudioManager.PlayRockerSound(AudioManager.RockerSound.BoulderLand, pointOfIntersection);

        }
        else if (other.CompareTag("Player"))
        {
            InstantiateLandFX(pointOfIntersection);
            AudioManager.PlayRockerSound(AudioManager.RockerSound.BoulderHit, pointOfIntersection);
        } 
    }

    void InstantiateLandFX(Vector3 FXPoint)
    {
        Instantiate(landFx, FXPoint, Quaternion.identity);
    }
    
    public void DestroyThisObj()
    {
        AudioManager.PlayRockerSound(AudioManager.RockerSound.BoulderLand, transform.position);
        Destroy(this.gameObject);
    }
}
