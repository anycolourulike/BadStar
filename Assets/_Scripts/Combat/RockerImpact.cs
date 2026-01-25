using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Attributes;
using Rambler.Core;

public class RockerImpact : MonoBehaviour
{
    [SerializeField] GameObject impactFX;
    [SerializeField] GameObject bounceFX;
    [SerializeField] GameObject boulderShards;
    MeshRenderer meshRenderer;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {           
            AudioManager.PlayRockerSound(AudioManager.RockerSound.BoulderHit, this.transform.position);
            meshRenderer.enabled = false;
            Instantiate(impactFX, this.transform.position, Quaternion.identity);
            Instantiate(boulderShards, this.transform.position, Quaternion.identity);
            Destroy(this.gameObject);
            Projectile thisProj = GetComponent<Projectile>();
            var damage = thisProj.GetDamage();
            var player = other.gameObject;
            var playerHealth = player.GetComponent<Health>();
            playerHealth.TakeDamage(damage);
        }

        if(other.CompareTag("plane"))
        {
            Vector3 pointOfIntersection = other.ClosestPoint(transform.position);
            Instantiate(bounceFX, pointOfIntersection, Quaternion.identity);
        }
    }

}
