using UnityEngine;
using Rambler.Attributes;

namespace Rambler
{
    //Put on a trigger volume at a doorway, a window or inside a room. While a character with one of
    //the listed tags is inside, its RoofFader fades the roof out. The volume's own mesh is hidden and,
    //if it has no collider yet, a box trigger is fitted to it.
    public class RoofFadeZone : MonoBehaviour
    {
        [Tooltip("The roof to fade. Left empty, it uses the RoofFader on this object's parent or siblings.")]
        [SerializeField] RoofFader roof;
        [SerializeField] string[] tags = { "Player", "Companion" };

        void Awake()
        {
            if (roof == null) roof = GetComponentInParent<RoofFader>();
            if (roof == null && transform.parent != null) roof = transform.parent.GetComponentInChildren<RoofFader>();
            if (roof == null) Debug.LogWarning("[RoofFadeZone] " + name + " has no RoofFader to fade", this);

            var col = GetComponent<Collider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>(); //Fits the mesh bounds
            col.isTrigger = true;

            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            //Trigger events need a Rigidbody on one side; a kinematic one here works with any character
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (roof != null && Counts(other)) roof.Enter(other);
        }

        void OnTriggerExit(Collider other)
        {
            if (roof != null) roof.Exit(other);
        }

        bool Counts(Collider other)
        {
            var health = other.GetComponentInParent<Health>();
            if (health != null && health.IsDead()) return false;
            var who = health != null ? health.gameObject : other.gameObject;
            foreach (var t in tags)
                if (who.CompareTag(t)) return true;
            return false;
        }
    }
}
