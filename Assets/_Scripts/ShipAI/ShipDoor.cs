using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Rambler.Attributes;

namespace Rambler.Ship
{
    //A door the ship can control one at a time. Opens for characters like DoorOpen does,
    //and the ship can seal it shut or slam it on whoever is standing in the doorway.
    //Put it on the object with the doorway trigger. Animate it with an Animator (Open and Close
    //triggers) or with sliding panels that move by their offsets, in this object's space, when open.
    [RequireComponent(typeof(Collider))]
    public class ShipDoor : MonoBehaviour
    {
        public enum DoorState { Closed, Open, Sealed }

        //Fired when a character steps into any doorway; the ship uses it to time a crush
        public static event Action<ShipDoor, GameObject> DoorwayEntered;
        public event Action<ShipDoor, DoorState> StateChanged;

        [SerializeField] Animator anim;
        [SerializeField] string openTrigger = "Open";
        [SerializeField] string closeTrigger = "Close";
        [SerializeField] float stayOpenTime = 4f;   //Closes this long after the doorway empties

        [Header("Sliding Panels (instead of an Animator)")]
        [SerializeField] Transform[] slidingPanels;
        [Tooltip("How far each panel moves when open, in this object's space, matched to Sliding Panels by index.")]
        [SerializeField] Vector3[] openOffsets;
        [SerializeField] float slideSpeed = 4f;

        [Header("Ship Control")]
        [SerializeField] float crushDamage = 150f;
        [Tooltip("Optional. Carves the NavMesh while sealed so AI stops pathing through.")]
        [SerializeField] NavMeshObstacle obstacle;
        [Tooltip("Optional. Shown while sealed, e.g. a red light.")]
        [SerializeField] GameObject sealedIndicator;

        readonly Dictionary<GameObject, int> inDoorway = new();
        readonly List<GameObject> doorwayList = new();
        DoorState state = DoorState.Closed;
        float emptyTimer;
        Vector3[] closedLocal;
        Vector3[] openLocal;

        public DoorState State => state;
        public bool IsSealed => state == DoorState.Sealed;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            anim = GetComponentInParent<Animator>();
        }

        void Awake()
        {
            if (anim == null) anim = GetComponentInParent<Animator>();
            if (obstacle != null) obstacle.enabled = false;
            if (sealedIndicator != null) sealedIndicator.SetActive(false);
            RecordPanels();
        }

        //Panels may sit anywhere in the model's hierarchy, so work out each one's open spot in its own parent's space
        void RecordPanels()
        {
            int count = slidingPanels != null ? slidingPanels.Length : 0;
            closedLocal = new Vector3[count];
            openLocal = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var panel = slidingPanels[i];
                if (panel == null) continue;
                closedLocal[i] = panel.localPosition;
                Vector3 offset = openOffsets != null && i < openOffsets.Length ? openOffsets[i] : Vector3.zero;
                Vector3 worldOffset = transform.TransformVector(offset);
                openLocal[i] = closedLocal[i] + (panel.parent != null ? panel.parent.InverseTransformVector(worldOffset) : worldOffset);
            }
        }

        void Update()
        {
            SlidePanels();
            if (state != DoorState.Open) return;
            if (DoorwayOccupants().Count > 0)
            {
                emptyTimer = 0f;
                return;
            }
            emptyTimer += Time.deltaTime;
            if (emptyTimer >= stayOpenTime) Close();
        }

        public void Open()
        {
            if (state != DoorState.Closed) return;
            SetState(DoorState.Open);
            emptyTimer = 0f;
            Fire(openTrigger, closeTrigger);
        }

        public void Close()
        {
            if (state != DoorState.Open) return;
            SetState(DoorState.Closed);
            Fire(closeTrigger, openTrigger);
        }

        public void SetSealed(bool sealedShut)
        {
            if (sealedShut)
            {
                if (state == DoorState.Sealed) return;
                if (state == DoorState.Open) Fire(closeTrigger, openTrigger);
                SetState(DoorState.Sealed);
            }
            else
            {
                if (state != DoorState.Sealed) return;
                SetState(DoorState.Closed);
            }
            if (obstacle != null) obstacle.enabled = sealedShut;
            if (sealedIndicator != null) sealedIndicator.SetActive(sealedShut);
        }

        //Slams the door on everyone in the doorway. Returns how many characters it hit.
        //The player's own crush spares their squad, and only fires when an enemy is there.
        public int Crush(bool sparePlayerSide = false)
        {
            var victims = new List<GameObject>(DoorwayOccupants()); //Copy: damage can kill and change the list
            if (sparePlayerSide) victims.RemoveAll(ShipCharacters.IsPlayerSide);
            if (victims.Count == 0) return 0;

            if (state == DoorState.Open) SetState(DoorState.Closed);
            Fire(closeTrigger, openTrigger);
            int hit = 0;
            foreach (var character in victims)
            {
                var health = character.GetComponent<Health>();
                if (health == null) continue;
                health.TakeDamage(crushDamage);
                hit++;
            }
            return hit;
        }

        public IReadOnlyList<GameObject> DoorwayOccupants()
        {
            doorwayList.Clear();
            foreach (var character in inDoorway.Keys)
            {
                if (ShipCharacters.IsAlive(character)) doorwayList.Add(character);
            }
            return doorwayList;
        }

        bool CanUse(GameObject character)
        {
            return character.CompareTag("Enemy") || character.CompareTag("Player") || character.CompareTag("Companion");
        }

        void OnTriggerEnter(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null) return;
            inDoorway.TryGetValue(character, out int count);
            inDoorway[character] = count + 1;
            if (count > 0) return; //Another collider of someone already inside

            DoorwayEntered?.Invoke(this, character);
            if (state == DoorState.Closed && CanUse(character)) Open();
        }

        void OnTriggerExit(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null || !inDoorway.TryGetValue(character, out int count)) return;
            if (count <= 1) inDoorway.Remove(character);
            else inDoorway[character] = count - 1;
        }

        void SlidePanels()
        {
            if (closedLocal == null) return;
            bool open = state == DoorState.Open;
            for (int i = 0; i < closedLocal.Length; i++)
            {
                var panel = slidingPanels[i];
                if (panel == null) continue;
                var target = open ? openLocal[i] : closedLocal[i];
                panel.localPosition = Vector3.MoveTowards(panel.localPosition, target, slideSpeed * Time.deltaTime);
            }
        }

        void SetState(DoorState next)
        {
            state = next;
            StateChanged?.Invoke(this, state);
        }

        //Clear the opposite trigger so quick open/close requests don't queue up in the Animator
        void Fire(string trigger, string opposite)
        {
            if (anim == null) return;
            anim.ResetTrigger(opposite);
            anim.SetTrigger(trigger);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = state == DoorState.Sealed ? Color.red : state == DoorState.Open ? Color.green : Color.white;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.3f);
        }
    }
}
