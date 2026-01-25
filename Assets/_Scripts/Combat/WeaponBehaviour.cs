using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Rambler.Combat
{
    public abstract class WeaponBehaviour: MonoBehaviour
    {         
        public int magazineAmount;
        public int totalAmmo;        
        public float reloadTime;        
        public int bulletsFired;       
        public  bool readyToShoot;
        public bool isShooting;        
        public bool reloading;             
    }
}
