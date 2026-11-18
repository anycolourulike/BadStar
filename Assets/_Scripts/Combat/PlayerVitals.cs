using System.Collections;
using System.Collections.Generic;
using Rambler.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Rambler.Attributes;
using Rambler.Saving;
using Rambler.Control;

namespace Rambler.Combat
{
    public class PlayerVitals : MonoBehaviour, ISaveable
    { 
        public float energyBurnRate = 0.1f;
        [SerializeField] Image healthbarFill;
        [SerializeField] Image energybarFill;
        [SerializeField] PlayerSelector selector;
        [SerializeField] Health health;
        [SerializeField] GameObject energyUI;
        
       public float SetEnergyBurnRate {set {energyBurnRate = value;}}
       public bool isHuman;
         
       float playerMaxEnergy = 100;              
       float playerMaxHP;      
       float playerCurEnergy;  
       float shownHealth = -1f;
      

       void Start()
       {
            if(isHuman == true)
            {
                if(energyUI != null)
                {
                   energyUI.SetActive(false);
                }
            }
            playerMaxHP = health.healthPoints.value;
            MaxEnergy();
       }

       void Update()
       {
            if(isHuman == false)
            {
                ReduceEnergy();
            }            
           
            SyncHealthBar();

            if (playerCurEnergy <= 0)
            {
                playerCurEnergy = 0;
                health.HealthCheck();
                if (health.IsDead() == true)
                {
                    return;
                }
            }
       }
        
        public void Restore() 
        {
            MaxHealth();
            MaxEnergy();
        }

        //Portrait slots are built at runtime and hand their bars over here
        public void BindBars(Image healthBar, Image energyBar)
        {
            healthbarFill = healthBar;
            energybarFill = energyBar;
            if (energybarFill != null) energybarFill.fillAmount = playerCurEnergy / playerMaxEnergy;
            shownHealth = -1f;
            SyncHealthBar();
        }

        //Health calls this when hit. The bar now follows Health's own value (see SyncHealthBar), which already
        //includes cover and every other modifier, so there is nothing to subtract here.
        public void TakeDamage(float weaponDamage)
        {
            SyncHealthBar();
        }

        //The health bar shows Health's real value, so it can't drift away from it
        void SyncHealthBar()
        {
            if (healthbarFill == null || health == null || playerMaxHP <= 0f) return;
            float hp = health.healthPoints.value;
            if (Mathf.Approximately(hp, shownHealth)) return;
            shownHealth = hp;
            healthbarFill.fillAmount = Mathf.Clamp01(hp / playerMaxHP);
        }

        public void MaxHealth()
        {
            health.healthPoints.value = playerMaxHP;  
            SyncHealthBar();
        }        

        void MaxEnergy()
        {
            playerCurEnergy = playerMaxEnergy;
            if (energybarFill != null) energybarFill.fillAmount = 1;
        }

        void ReduceEnergy()
        {
            playerCurEnergy -= energyBurnRate * Time.deltaTime;
            var fillEnergy = playerCurEnergy / 100;
            if (energybarFill != null) energybarFill.fillAmount = fillEnergy;
        }

        //Health saves the real value and the bar follows it; kept so old saves still load
        public object CaptureState()
        {
            return healthbarFill != null ? healthbarFill.fillAmount : 1f;
        }

        public void RestoreState(object state)
        {
            shownHealth = -1f; //Redraw from Health on the next frame
        }
    }
}
