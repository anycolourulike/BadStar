using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //Lets a ShipDoor be shot to pieces. Each sliding panel has two hidden children, "<panel>_Damaged" and
    //"<panel>_Destroyed"; the panels swap to the damaged look at damagedAt of their health and to the wreck at 0.
    //A destroyed door is open for good (see ShipDoor.Break) and, while the ship is hostile, raises the alarm.
    //The hitbox is only solid while the door is shut, so it never blocks the open doorway.
    [RequireComponent(typeof(ShipDoor))]
    public class ShipDoorDamage : MonoBehaviour
    {
        public enum DamageState { Intact, Damaged, Destroyed }

        public static event Action<ShipDoorDamage> Destroyed;
        public event Action<ShipDoorDamage, DamageState> DamageStateChanged;

        [SerializeField] float maxHealth = 120f;
        [Tooltip("Switches to the damaged look at or below this fraction of health.")]
        [Range(0f, 1f)] [SerializeField] float damagedAt = 0.5f;
        [Tooltip("Child with CombatTarget + BoxCollider + ShipDoorHitbox, so the door can be targeted and shot.")]
        [SerializeField] Collider hitbox;
        [Tooltip("Optional effect spawned when the door is destroyed.")]
        [SerializeField] GameObject destroyFX;

        const string DamagedSuffix = "_Damaged";
        const string DestroyedSuffix = "_Destroyed";

        ShipDoor door;
        float health;
        readonly List<Renderer> intact = new();
        readonly List<Renderer> damaged = new();
        readonly List<Renderer> wrecked = new();

        public DamageState State { get; private set; }
        public float Health => health;
        public float MaxHealth => maxHealth;
        public Collider Hitbox => hitbox;
        public ShipDoor Door => door;

        void Awake()
        {
            door = GetComponent<ShipDoor>();
            health = maxHealth;
            if (door.Panels != null)
            {
                foreach (var panel in door.Panels)
                {
                    if (panel == null) continue;
                    var r = panel.GetComponent<Renderer>();
                    if (r != null) intact.Add(r);
                    AddVariant(panel, panel.name + DamagedSuffix, damaged);
                    AddVariant(panel, panel.name + DestroyedSuffix, wrecked);
                }
            }
            ShowLook();
            UpdateHitbox(door.State);
        }

        static void AddVariant(Transform panel, string childName, List<Renderer> into)
        {
            var child = panel.Find(childName);
            if (child == null) return;
            var r = child.GetComponent<Renderer>();
            if (r != null) into.Add(r);
        }

        void OnEnable() => GetComponent<ShipDoor>().StateChanged += OnDoorStateChanged;
        void OnDisable() => GetComponent<ShipDoor>().StateChanged -= OnDoorStateChanged;

        void OnDoorStateChanged(ShipDoor _, ShipDoor.DoorState state) => UpdateHitbox(state);

        //Only a shut door can be shot; an open one would block the doorway
        void UpdateHitbox(ShipDoor.DoorState state)
        {
            if (hitbox != null) hitbox.enabled = State != DamageState.Destroyed && state != ShipDoor.DoorState.Open;
        }

        public void TakeDamage(float amount)
        {
            if (State == DamageState.Destroyed || amount <= 0f) return;
            health -= amount;
            if (health <= 0f) Break();
            else if (State == DamageState.Intact && health <= maxHealth * damagedAt) SetState(DamageState.Damaged);
        }

        void Break()
        {
            health = 0f;
            SetState(DamageState.Destroyed);
            door.Break();
            UpdateHitbox(door.State);
            if (destroyFX != null) Instantiate(destroyFX, transform.position, Quaternion.identity);

            var room = ShipRoom.At(transform.position);
            Debug.Log("[ShipAI] Door destroyed: " + name);
            if (ShipAI.Instance != null) ShipAI.Instance.RaiseAlarm(transform.position, room, "door destroyed");
            Destroyed?.Invoke(this);
        }

        void SetState(DamageState next)
        {
            State = next;
            ShowLook();
            DamageStateChanged?.Invoke(this, State);
        }

        //A door without damaged/wrecked models keeps its intact look rather than vanishing
        void ShowLook()
        {
            bool showDamaged = State == DamageState.Damaged && damaged.Count > 0;
            bool showWrecked = State == DamageState.Destroyed && wrecked.Count > 0;
            foreach (var r in intact) r.enabled = !showDamaged && !showWrecked;
            foreach (var r in damaged) r.enabled = showDamaged;
            foreach (var r in wrecked) r.enabled = showWrecked;
        }
    }
}
