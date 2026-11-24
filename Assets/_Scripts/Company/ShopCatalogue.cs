using System;
using UnityEngine;
using Rambler.Combat;

namespace Rambler.Crew
{
    //What the Home Terminal's shop sells. Upgrades carry their own price; weapons are priced here (the Weapon
    //asset has no price). Lives at Resources/Company/Shop Catalogue.
    [CreateAssetMenu(fileName = "Shop Catalogue", menuName = "Rambler/Crew/Shop Catalogue")]
    public class ShopCatalogue : ScriptableObject
    {
        [Serializable]
        public struct WeaponForSale
        {
            public Weapon weapon;
            [Min(0)] public int price;
        }

        [SerializeField] UpgradeDefinition[] upgrades = new UpgradeDefinition[0];
        [SerializeField] WeaponForSale[] weapons = new WeaponForSale[0];

        public UpgradeDefinition[] Upgrades => upgrades;
        public WeaponForSale[] Weapons => weapons;
    }
}
