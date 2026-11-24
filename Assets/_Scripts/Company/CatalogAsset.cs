using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ownables
{
    //Base for every authored asset the save file refers to by ID (skills, characters, courses, later ships,
    //businesses and upgrades). Same pattern as Weapon: an auto-generated GUID, and a lookup over every asset
    //of this kind kept in a Resources folder. Assets outside a Resources folder can't be found from a save.
    public abstract class CatalogAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [Tooltip("Auto-generated ID for saving/loading. Clear it to generate a new one (do this after duplicating an asset).")]
        [SerializeField] string itemID = null;
        [SerializeField] string displayName = null;
        [SerializeField] [TextArea] string description = null;
        [SerializeField] Sprite icon = null;

        static Dictionary<string, CatalogAsset> lookup;

        public string ItemID => itemID;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;

        public static T GetFromID<T>(string id) where T : CatalogAsset
        {
            if (string.IsNullOrEmpty(id)) return null;
            BuildLookup();
            return lookup.TryGetValue(id, out var asset) ? asset as T : null;
        }

        public static IEnumerable<T> All<T>() where T : CatalogAsset
        {
            BuildLookup();
            foreach (var asset in lookup.Values)
                if (asset is T t) yield return t;
        }

        static void BuildLookup()
        {
            if (lookup != null) return;
            lookup = new Dictionary<string, CatalogAsset>();
            foreach (var asset in Resources.LoadAll<CatalogAsset>(""))
            {
                if (string.IsNullOrEmpty(asset.itemID)) continue;
                if (lookup.TryGetValue(asset.itemID, out var other))
                {
                    Debug.LogError("Duplicate catalog ID on " + other.name + " and " + asset.name + ". Clear the ID on the copy.", asset);
                    continue;
                }
                lookup[asset.itemID] = asset;
            }
        }

        //Editor play mode can skip the domain reload; rebuild the lookup each session so new assets are found
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLookup() => lookup = null;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (string.IsNullOrWhiteSpace(itemID)) itemID = System.Guid.NewGuid().ToString();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize() { }
    }
}
