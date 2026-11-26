using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Crew.Battle
{
    //A crate of cargo in the ship's hold. Boarders carry crates off to their airlock; every crate lost is
    //taken off the battle's pay. Put on the hold's crates by Tools > Company > Build Shearwater Interior.
    public class CargoCrate : MonoBehaviour
    {
        static readonly List<CargoCrate> all = new List<CargoCrate>();
        public static IReadOnlyList<CargoCrate> All => all;

        [Tooltip("Credits lost if a boarder gets away with it.")]
        [SerializeField] int value = 120;

        public int Value => value;
        public bool Taken { get; private set; }
        //A boarder on its way to take it (so two don't go for the same crate)
        public GameObject ClaimedBy { get; set; }
        public bool Free => !Taken && (ClaimedBy == null || !Rambler.Ship.ShipCharacters.IsAlive(ClaimedBy));

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        //Picked up: the crate disappears from the hold
        public void Take()
        {
            Taken = true;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        //Dropped (the carrier fell): back where it was
        public void PutBack()
        {
            Taken = false;
            ClaimedBy = null;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        }
    }
}
