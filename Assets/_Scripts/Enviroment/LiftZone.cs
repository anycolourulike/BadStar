using System.Collections.Generic;
using UnityEngine;
using Rambler.Ship;

namespace Rambler.Mine
{
    //Trigger in front of a lift landing or train platform. Counts characters by root (they have several colliders) and
    //raises 'changed' so the owner can show or hide its panel as the player comes and goes.
    [RequireComponent(typeof(Collider))]
    public class LiftZone : MonoBehaviour
    {
        [HideInInspector] public int stopIndex;
        public event System.Action changed;

        readonly Dictionary<GameObject, int> inside = new Dictionary<GameObject, int>();
        readonly List<GameObject> list = new List<GameObject>();

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null) return;
            inside.TryGetValue(character, out int count);
            inside[character] = count + 1;
            if (count == 0) changed?.Invoke();
        }

        void OnTriggerExit(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null || !inside.TryGetValue(character, out int count)) return;
            if (count <= 1) inside.Remove(character); else inside[character] = count - 1;
            if (count <= 1) changed?.Invoke();
        }

        //Riders are teleported in and out, so they never fire exit events; forget anyone no longer actually here
        public bool Contains(GameObject character)
        {
            Prune();
            return character != null && inside.ContainsKey(character);
        }

        public List<GameObject> Occupants()
        {
            Prune();
            list.Clear();
            foreach (var c in inside.Keys) if (ShipCharacters.IsAlive(c)) list.Add(c);
            return list;
        }

        void Prune()
        {
            var bounds = GetComponent<Collider>().bounds;
            bounds.Expand(0.5f);
            var gone = new List<GameObject>();
            foreach (var c in inside.Keys) if (c == null || !bounds.Contains(c.transform.position)) gone.Add(c);
            foreach (var c in gone) inside.Remove(c);
        }
    }
}
