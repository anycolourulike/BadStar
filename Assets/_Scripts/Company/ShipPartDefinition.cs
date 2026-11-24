using UnityEngine;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //A ship upgrade: bought at the Shipyard (Parts tab), kept in the Company's spare parts until fitted,
    //one per slot. Keep these in a Resources folder; every part there is for sale.
    [CreateAssetMenu(fileName = "Ship Part", menuName = "Rambler/Company/Ship Part")]
    public class ShipPartDefinition : OwnableDefinition
    {
        [SerializeField] ShipSlot slot = ShipSlot.Engine;
        [SerializeField] ShipStats stats;
        [Tooltip("A support ability the squad can call in from the map while this ship is active.")]
        [SerializeField] SupportAbility ability = SupportAbility.None;

        public ShipSlot Slot => slot;
        public ShipStats Stats => stats;
        public SupportAbility Ability => ability;

        public string Describe()
        {
            string s = stats.Describe();
            if (ability != SupportAbility.None) s = (s.Length > 0 ? s + "  " : "") + ability + " ability";
            return s;
        }
    }
}
