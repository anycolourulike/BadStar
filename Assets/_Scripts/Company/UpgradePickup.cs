using UnityEngine;
using Rambler.Combat;
using Rambler.Ship;

namespace Rambler.Crew
{
    //Loot: an attachment, a piece of gear or a gun lying in a level. A squad member walking into its trigger
    //sends it to the Company (upgrades to the stash, guns to the armoury), where it is fitted at the Home Terminal.
    [RequireComponent(typeof(Collider))]
    public class UpgradePickup : MonoBehaviour
    {
        [SerializeField] UpgradeDefinition upgrade;
        [Tooltip("Or a gun instead of an upgrade.")]
        [SerializeField] Weapon weapon;
        [Tooltip("Spins and bobs so it reads as loot.")]
        [SerializeField] bool animate = true;

        Vector3 home;

        public void Set(UpgradeDefinition u, Weapon w) { upgrade = u; weapon = w; }

        void Start()
        {
            GetComponent<Collider>().isTrigger = true;
            home = transform.position;
        }

        void Update()
        {
            if (!animate) return;
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
            transform.position = home + Vector3.up * (0.1f * Mathf.Sin(Time.time * 2f));
        }

        void OnTriggerEnter(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null || !ShipCharacters.IsPlayerSide(character)) return;
            var company = Company.Instance;
            if (company == null) return;
            if (upgrade != null) company.AddToStash(upgrade);
            if (weapon != null) company.AddToArmoury(weapon.GetItemID());
            Debug.Log("[Loot] " + character.name + " picked up " + (upgrade != null ? upgrade.DisplayName : weapon != null ? weapon.name : "nothing"));
            Destroy(gameObject);
        }
    }
}
