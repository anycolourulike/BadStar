using System;
using System.Collections.Generic;
using Rambler.Combat;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //One actual gun: which Weapon asset it is plus the attachments fitted to it. Attachments stay with the gun
    //when it goes to the armoury or to another crew member.
    [Serializable]
    public class WeaponItem
    {
        public string weaponId;
        public List<string> attachmentIds = new List<string>();

        public WeaponItem() { }
        public WeaponItem(string weaponId) { this.weaponId = weaponId; }

        public Weapon Weapon => Weapon.GetFromID(weaponId);

        public UpgradeDefinition AttachmentIn(UpgradeSlot slot)
        {
            foreach (var id in attachmentIds)
            {
                var u = CatalogAsset.GetFromID<UpgradeDefinition>(id);
                if (u != null && u.Slot == slot) return u;
            }
            return null;
        }

        public ModifierSet Modifiers
        {
            get
            {
                var total = new ModifierSet();
                foreach (var id in attachmentIds)
                {
                    var u = CatalogAsset.GetFromID<UpgradeDefinition>(id);
                    if (u != null) total += u.Modifiers;
                }
                return total;
            }
        }
    }
}
