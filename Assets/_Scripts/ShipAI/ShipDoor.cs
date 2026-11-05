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
    //triggers), with sliding panels that move by their offsets, in this object's space, when open,
    //or with hinged panels that swing about a hinge line (a ramp, a hatch).
    [RequireComponent(typeof(Collider))]
    public class ShipDoor : MonoBehaviour
    {
        public enum DoorState { Closed, Open, Sealed }

        static readonly List<ShipDoor> all = new();
        public static IReadOnlyList<ShipDoor> All => all;

        //Fired when a character steps into any doorway; the ship uses it to time a crush
        public static event Action<ShipDoor, GameObject> DoorwayEntered;
        public event Action<ShipDoor, DoorState> StateChanged;

        [Tooltip("Optional. Shown as the door panel's title when the door isn't between ship rooms, e.g. a yard gate.")]
        [SerializeField] string displayName;
        [SerializeField] Animator anim;
        [SerializeField] string openTrigger = "Open";
        [SerializeField] string closeTrigger = "Close";
        [SerializeField] float stayOpenTime = 4f;   //Closes this long after the doorway empties; 0 = stays open until told to close
        [Tooltip("Starts the level open, e.g. a ramp that is already down.")]
        [SerializeField] bool startOpen;

        [Header("Sliding Panels (instead of an Animator)")]
        [SerializeField] Transform[] slidingPanels;
        [Tooltip("How far each panel moves when open, in this object's space, matched to Sliding Panels by index.")]
        [SerializeField] Vector3[] openOffsets;
        [SerializeField] float slideSpeed = 4f;

        [Header("Hinged Panels (swing open, e.g. a ramp)")]
        [SerializeField] Transform[] hingedPanels;
        [Tooltip("A point on the hinge line, in this object's space.")]
        [SerializeField] Vector3 hingePoint;
        [Tooltip("Direction of the hinge line, in this object's space.")]
        [SerializeField] Vector3 hingeAxis = Vector3.right;
        [Tooltip("How far the panels swing about the hinge when open (degrees, sign gives the direction).")]
        [SerializeField] float openAngle = 90f;
        [Tooltip("Degrees per second.")]
        [SerializeField] float swingSpeed = 120f;

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
        Vector3[] hingeClosedPos, hingeLocalPoint, hingeLocalAxis;
        Quaternion[] hingeClosedRot;
        float swing; //0 shut .. 1 fully open
        List<Transform> panels;

        public DoorState State => state;
        public string DisplayName => displayName;
        public bool IsSealed => state == DoorState.Sealed;
        //Blown open for good: stays open and ignores close, seal and crush
        public bool IsBroken { get; private set; }
        //Every moving part of the door, sliding and hinged
        public IReadOnlyList<Transform> Panels
        {
            get
            {
                if (panels == null)
                {
                    panels = new List<Transform>();
                    if (slidingPanels != null) panels.AddRange(slidingPanels);
                    if (hingedPanels != null) panels.AddRange(hingedPanels);
                }
                return panels;
            }
        }

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            anim = GetComponentInParent<Animator>();
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        //Size of the doorway, taken from the doorway trigger (width, height)
        public Vector2 OpeningSize
        {
            get
            {
                var box = GetComponent<BoxCollider>();
                return box != null ? new Vector2(box.size.x, box.size.y) : new Vector2(2f, 2.7f);
            }
        }

        void Awake()
        {
            if (anim == null) anim = GetComponentInParent<Animator>();
            if (obstacle != null) obstacle.enabled = false;
            if (sealedIndicator != null) sealedIndicator.SetActive(false);
            RecordPanels();
            if (startOpen)
            {
                state = DoorState.Open;
                for (int i = 0; i < closedLocal.Length; i++)
                {
                    if (slidingPanels[i] != null) slidingPanels[i].localPosition = openLocal[i];
                }
                swing = 1f;
                for (int i = 0; i < hingeClosedPos.Length; i++)
                {
                    if (hingedPanels[i] != null) PoseHinged(i, 1f);
                }
            }
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

            //Hinged panels swing about the hinge line, worked out in each panel's parent space
            int hinged = hingedPanels != null ? hingedPanels.Length : 0;
            hingeClosedPos = new Vector3[hinged];
            hingeClosedRot = new Quaternion[hinged];
            hingeLocalPoint = new Vector3[hinged];
            hingeLocalAxis = new Vector3[hinged];
            Vector3 worldPoint = transform.TransformPoint(hingePoint), worldAxis = transform.TransformDirection(hingeAxis);
            for (int i = 0; i < hinged; i++)
            {
                var panel = hingedPanels[i];
                if (panel == null) continue;
                hingeClosedPos[i] = panel.localPosition;
                hingeClosedRot[i] = panel.localRotation;
                hingeLocalPoint[i] = panel.parent != null ? panel.parent.InverseTransformPoint(worldPoint) : worldPoint;
                hingeLocalAxis[i] = (panel.parent != null ? panel.parent.InverseTransformDirection(worldAxis) : worldAxis).normalized;
            }
        }

        //Places hinged panel i a fraction of the way open
        void PoseHinged(int i, float fraction)
        {
            var turn = Quaternion.AngleAxis(openAngle * fraction, hingeLocalAxis[i]);
            hingedPanels[i].localRotation = turn * hingeClosedRot[i];
            hingedPanels[i].localPosition = hingeLocalPoint[i] + turn * (hingeClosedPos[i] - hingeLocalPoint[i]);
        }

        void Update()
        {
            if (IsBroken) return; //The wreckage stays where it was
            SlidePanels();
            if (state != DoorState.Open) return;
            if (DoorwayOccupants().Count > 0)
            {
                emptyTimer = 0f;
                return;
            }
            emptyTimer += Time.deltaTime;
            if (stayOpenTime > 0f && emptyTimer >= stayOpenTime) Close();
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
            if (state != DoorState.Open || IsBroken) return;
            SetState(DoorState.Closed);
            Fire(closeTrigger, openTrigger);
        }

        public void SetSealed(bool sealedShut)
        {
            if (IsBroken) return;
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
            if (IsBroken) return 0;
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

        //Destroyed: open for good, no longer blocks pathing, and the sealed light goes out
        public void Break()
        {
            if (IsBroken) return;
            IsBroken = true;
            if (obstacle != null) obstacle.enabled = false;
            if (sealedIndicator != null) sealedIndicator.SetActive(false);
            SetState(DoorState.Open);
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

            if (hingeClosedPos == null || hingeClosedPos.Length == 0) return;
            float goal = open ? 1f : 0f;
            if (Mathf.Approximately(swing, goal)) return;
            swing = Mathf.MoveTowards(swing, goal, swingSpeed / Mathf.Max(1f, Mathf.Abs(openAngle)) * Time.deltaTime);
            for (int i = 0; i < hingeClosedPos.Length; i++)
            {
                if (hingedPanels[i] != null) PoseHinged(i, swing);
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
