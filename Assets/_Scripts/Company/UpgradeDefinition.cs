using System;
using UnityEngine;
using Rambler.Combat;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //Where an upgrade fits. The first four go on a weapon; Armour and Implant are a person's gear.
    //Ship slots (hardpoints, engines, shields) will be added here.
    public enum UpgradeSlot { Scope, Barrel, Magazine, Underbarrel, Armour, Implant }

    //Which guns an attachment fits
    [Flags]
    public enum WeaponFamily { None = 0, Pistol = 1, SMG = 2, Rifle = 4, Shotgun = 8, Launcher = 16, All = 31 }

    //An attachment or a piece of gear: bought in the Home Terminal's shop, found as loot or given as a mission
    //reward, then fitted in a crew member's Loadout. Keep these in a Resources folder so saves can find them.
    [CreateAssetMenu(fileName = "Upgrade", menuName = "Rambler/Crew/Upgrade")]
    public class UpgradeDefinition : OwnableDefinition
    {
        [SerializeField] UpgradeSlot slot = UpgradeSlot.Scope;
        [Tooltip("Guns this attachment fits (ignored for gear).")]
        [SerializeField] WeaponFamily fits = WeaponFamily.All;
        [SerializeField] ModifierSet modifiers;

        public UpgradeSlot Slot => slot;
        public WeaponFamily Fits => fits;
        public ModifierSet Modifiers => modifiers;
        public bool IsAttachment => IsWeaponSlot(slot);
        public bool IsGear => !IsWeaponSlot(slot);

        public static bool IsWeaponSlot(UpgradeSlot s) => s == UpgradeSlot.Scope || s == UpgradeSlot.Barrel || s == UpgradeSlot.Magazine || s == UpgradeSlot.Underbarrel;
        public static readonly UpgradeSlot[] WeaponSlots = { UpgradeSlot.Scope, UpgradeSlot.Barrel, UpgradeSlot.Magazine, UpgradeSlot.Underbarrel };
        public static readonly UpgradeSlot[] GearSlots = { UpgradeSlot.Armour, UpgradeSlot.Implant };

        public bool FitsWeapon(Weapon weapon)
        {
            if (!IsAttachment || weapon == null) return false;
            var family = FamilyOf(weapon);
            return family != WeaponFamily.None && (fits & family) != 0;
        }

        //What kind of gun a Weapon asset is (melee and unarmed take no attachments)
        public static WeaponFamily FamilyOf(Weapon w)
        {
            if (w == null || w.isMelee || w.isMedKit || w.projectile == null) return WeaponFamily.None;
            if (w.isGrenadeLauncher) return WeaponFamily.Launcher;
            string t = ((w.weaponTitle ?? "") + " " + w.name).ToLowerInvariant();
            if (t.Contains("smg")) return WeaponFamily.SMG;
            if (t.Contains("shotgun")) return WeaponFamily.Shotgun;
            if (w.isPistol || t.Contains("pistol")) return WeaponFamily.Pistol;
            if (w.isRifle || t.Contains("rifle")) return WeaponFamily.Rifle;
            return WeaponFamily.Rifle;
        }
    }
}
