using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rambler.SceneManagement;
using Rambler.Saving;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Utils;
using System;
using Random = UnityEngine.Random;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;
using Rambler.Stats;
using TMPro;

namespace Rambler.Attributes // Add Player & Companion Death SFX
{
    public class Health : MonoBehaviour, ISaveable
    {        
        public LazyValue<float> healthPoints; 
        LazyValue<float> HealthPoints { get{return healthPoints;} set{healthPoints = value;}}
        
        [SerializeField] GameObject playerCore;
        [SerializeField] PlayerSelector playerSelector;
        [SerializeField] Button activateBtn;
        [SerializeField] Image playerButtonX;
        [SerializeField] GameObject RigLayer;
        [SerializeField] GameObject headFX;
        [SerializeField] GameObject legFX; 
        [SerializeField] GameObject armFX;
        [SerializeField] GameObject shield;
        [SerializeField] Mover mover;
        
        public delegate void TargetDeath();
        public static event TargetDeath targetDeath;
        public delegate void PlayerDied(GameObject obj);
        public static event PlayerDied playerDeath;
        public delegate void CompanionDied(GameObject obj);
        public static event CompanionDied companionDeath;
        public delegate void AIHit();
        public static event AIHit aIHit;
        UnityEngine.AI.NavMeshAgent agent;
        public float healthPts;
        bool isActivePlayer;
        CombatTarget combatTarget;
        EnemySpawner enemySpawner;
        GameObject hitScreenFX;
        CapsuleCollider capCol;
        SavingWrapper wrapper;
        float isDeadTimer = 0f;
        PlayerVitals vitals;
        public bool isDead;
        TimbertoesFOV TFOV;
        Image deathX;
        FieldOfView FOV;        
        Fighter fighter;
        float damage;        
        Animator anim;
        int dieRanNum;

        //Add death noise for Robot Players

        void Awake() 
        {
            healthPoints = new LazyValue<float>(GetInitialHealth);
            anim = GetComponent<Animator>();
        }

        void Start() 
        {
            playerCore = GameObject.Find("PlayerCore");
            playerSelector = playerCore.GetComponent<PlayerSelector>();
            wrapper = GetComponent<SavingWrapper>();
            FOV = GetComponent<FieldOfView>();
            combatTarget = GetComponent<CombatTarget>();
            enemySpawner = FindObjectOfType<EnemySpawner>();
            fighter = GetComponent<Fighter>(); 
            capCol = GetComponent<CapsuleCollider>();  
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (this.gameObject.CompareTag("Player"))
            {
                hitScreenFX = GameObject.Find(name: "/PlayerCore/HUD/DamageScreen");
                hitScreenFX.SetActive(false);
                vitals = GetComponent<PlayerVitals>();
            }
        }       

        void Update()
        {
            isDeadTimer += Time.deltaTime;
            healthPts = healthPoints.value;
        }

        public bool IsDead()
        {
            return isDead;
        } 

        public void TakeDamage(float damage)
        {  
            if (isDead == true) return;
            if (this.gameObject.CompareTag("Enemy") || (this.gameObject.CompareTag("Companion")))
            {
                aIHit?.Invoke();
            }            

            if (this.gameObject.CompareTag("Player") || (this.gameObject.CompareTag("Companion")))
            {
                if (vitals != null)
                {
                    StartCoroutine("HitFX");
                    vitals.TakeDamage(damage);
                    if (isDead == true) return;
                    anim.SetTrigger("HitAnim");
                }
            }          

            healthPoints.value = Mathf.Max(healthPoints.value - damage, 0);
            mover.CancelNav();
            HitAnim();
            HealthCheck();
        }

        public void HealthCheck()
        {
            //Debug.Log("HealthCheckCalled" + " " + gameObject.name + healthPoints.value);
            if (healthPoints.value <= 0)
            {
                if (gameObject.name == ("SuicideDrone"))
                {
                    var dronCon = GetComponent<DroneCon>();
                    dronCon.DroneDeath();
                    return;
                }
                enemySpawner.RemoveEnemy(gameObject);
                healthPoints.value = 0f;
                if (isDeadTimer <= 1f)
                { 
                    Destroy(gameObject);
                }
                else
                {
                    Die();
                }
            }            
        } 
        
        public void RestoreHealth()
        {
            healthPoints = new LazyValue<float>(GetInitialHealth);
            vitals.Restore();
        }
 
        public void Die()
        {
            //disable Button effect
            
            deathX.enabled = true;
            isDead = true;
            CheckTag(this.gameObject);           

            var rigBuilder = GetComponent<RigBuilder>();
            rigBuilder.enabled = false;
            var capCol = GetComponent<CapsuleCollider>();
            capCol.enabled = false;
            fighter.enabled = false;
            if (activateBtn != null)
            {
                activateBtn.enabled = false;
                playerButtonX.enabled = true;
            }

            anim.SetBool("HitAnim", false);
            dieRanNum = Random.Range(1, 4);            
            
            transform.position += new Vector3(0,0,-2f) * 10f * Time.deltaTime;
            if(dieRanNum == 1)
            {
                anim.speed = 2.5f;
                anim.SetTrigger("Die1");
                armFX.transform.SetParent(null);
                armFX.SetActive(true);
                if(this.CompareTag("Enemy"))
                {                  
                  AudioManager.PlayHumanSound(AudioManager.HumanSound.Death1, this.transform.position);
                  this.tag = "Dead";                  
                  targetDeath?.Invoke();
                    return;
                }                                             
            }
            else if (dieRanNum == 2)
            {
                anim.speed = 2.5f;
                anim.SetTrigger("Die2");
                headFX.SetActive(true);
                if(this.CompareTag("Enemy"))
                {                  
                  AudioManager.PlayHumanSound(AudioManager.HumanSound.Death2, this.transform.position); 
                  this.tag = "Dead";
                  targetDeath?.Invoke();
                    return;
                }                                                           
            }
            else if (dieRanNum == 3)
            {
                anim.speed = 2.5f;
                anim.SetTrigger("Die3");
                legFX.SetActive(true); 
                if(this.CompareTag("Enemy"))
                {
                  AudioManager.PlayHumanSound(AudioManager.HumanSound.Death3, this.transform.position); 
                  this.tag = "Dead";
                  targetDeath?.Invoke();
                    return;
                }                
            }                     
        }

        public void CheckTag(GameObject obj)
        {           
            string objTag = obj.tag;
            switch (objTag)
            {
                case "Enemy":
                    //Debug.Log("Object is an Enemy.");
                    var aiCon = GetComponent<AIController>();
                    aiCon.isDead = true;
                    Transform selectionUI = transform.Find("SelectionIcon");
                    var selObj = selectionUI.gameObject;
                    selObj.SetActive(false);
                    break;
                case "Companion":
                    //Debug.Log("Object is a Companion.");
                    shield.SetActive(false);
                    companionDeath?.Invoke(this.gameObject);                                     
                    break;
                case "Player":
                    //Debug.Log("Object is a Player.");
                    shield.SetActive(false);
                    playerDeath?.Invoke(this.gameObject);
                    break;
                default:
                    Debug.Log("Object has an unknown tag.");
                    // Perform default actions or handle unknown tags
                    break;
            }

        }

        public void HitTheFloor() 
        {
            AudioManager.PlayHumanSound(AudioManager.HumanSound.HumanHitGroundDeath, this.transform.position);
        }  

        public object CaptureState()
        {
            //Debug.Log(this.gameObject.name + " " + HealthPoints.value);
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["healthPoints.value"] = healthPoints.value;            
            return data;
        }

        public void RestoreState(object state)
        {
            Dictionary<string, object> data = (Dictionary<string, object>)state;
            healthPoints.value = (float)data["healthPoints.value"];                      
            HealthCheck();
        }

        public void DeathSpeedNormal()
        {
            anim.speed = 1f;
        }

        float GetInitialHealth()
        {
            return GetComponent<BaseStats>().GetStat(Stat.Health);
        }

        void HitAnim()
        {  
            if(isDead) return;
            if(gameObject.name == ("SuicideDrone"))
            {
                var droneCon = GetComponent<DroneCon>();
                droneCon.DroneDeath();
                return;
            }
            anim.SetTrigger("HitAnim");
            if(gameObject.CompareTag("Player")) return; 
            FOV.radius = 40f;
            AudioManager.PlayHumanSound(AudioManager.HumanSound.Hit1, position: this.transform.position); 
        }
        
        void OnParticleCollision(GameObject particleProj)
        {           
            var proj = particleProj.GetComponent<Projectile>();
            damage = proj.GetDamage();
            TakeDamage(damage);

            if (proj.HitEffect() != null)
            {                    
              var cloneProjectile = Instantiate(proj.HitEffect(), proj.GetAimLocation(), particleProj.transform.rotation);                      
              
              Destroy(proj.gameObject);
               //MF_AutoPool.Despawn(gameObject);     
            }
            else
            {
              Destroy(proj.gameObject);
               //MF_AutoPool.Despawn(gameObject);
            }       
        }          

        IEnumerator HitFX()
        {
            hitScreenFX.SetActive(true);
            yield return new WaitForSeconds(0.3f);
            hitScreenFX.SetActive(false);
        }
    }
}