using UnityEngine;

namespace Rambler.Ownables
{
    //Something the player can buy or hire: crew now; ships, businesses and upgrades later
    public abstract class OwnableDefinition : CatalogAsset
    {
        [Tooltip("Purchase price, or the hire fee for crew.")]
        [Min(0)] [SerializeField] int price = 0;

        public int Price => price;
    }
}
