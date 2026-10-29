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
       float playerCurHP;
       float fillDecrease;
      

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
            playerCurHP = health.healthPoints.value;
            MaxEnergy();
       }

       void Update()
       {
            if(isHuman == false)
            {
                ReduceEnergy();
            }            
           
            if (playerCurHP <= 0f)
            {
                playerCurHP = 0f;
                health.HealthCheck();
                if (health.IsDead() == true)
                {
                    return;
                }
            }

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
        }

        public void TakeDamage(float weaponDamage)
        {
            fillDecrease = weaponDamage / playerMaxHP;
            if (healthbarFill != null) healthbarFill.fillAmount -= fillDecrease;
        }      

        public void MaxHealth()
        {
            health.healthPoints.value = playerMaxHP;  
            if (healthbarFill != null) healthbarFill.fillAmount = 1;         
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

        public object CaptureState()
        {
            return healthbarFill != null ? healthbarFill.fillAmount : 1f;
        }

        public void RestoreState(object state)
        {
            if (healthbarFill != null) healthbarFill.fillAmount = (float)state;
        }
    }
}
