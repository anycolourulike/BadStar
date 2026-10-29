using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //A room's security camera: its ShipSensor watches for the player, and the player can shoot it.
    //Destroying it blinds the room (no sightings, and the ship map can't show who is inside) and,
    //while the ship is hostile and still has its sensors, sets off an alarm that brings it to Alert.
    [RequireComponent(typeof(ShipSensor))]
    public class ShipCamera : MonoBehaviour
    {
        static readonly List<ShipCamera> all = new();
        public static IReadOnlyList<ShipCamera> All => all;
        public static event Action<ShipCamera> Destroyed;

        [SerializeField] float maxHealth = 30f;
        [Tooltip("Shown while working.")]
        [SerializeField] GameObject intactModel;
        [Tooltip("Optional. Shown once destroyed.")]
        [SerializeField] GameObject brokenModel;
        [Tooltip("Optional effect spawned when destroyed.")]
        [SerializeField] GameObject destroyFX;
        [Tooltip("Child with CombatTarget + CapsuleCollider + ShipCameraHitbox, so the player can target and shoot the camera.")]
        [SerializeField] Transform hitbox;
        [Tooltip("Shooters aim this far above a target's position, so the hitbox sits this far below the lens.")]
        [SerializeField] float aimOffset = 0.9f;

        ShipSensor sensor;
        float health;

        public bool IsDestroyed { get; private set; }
        public ShipSensor Sensor => sensor;
        public ShipRoom Room => ShipRoom.At(transform.position);

        void Awake()
        {
            sensor = GetComponent<ShipSensor>();
            health = maxHealth;
            PlaceHitbox();
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        //Shooters aim at the target's position plus aimOffset straight up, so keep the hitbox
        //upright and below the lens whatever angle the camera is mounted at
        void PlaceHitbox()
        {
            if (hitbox == null) return;
            hitbox.SetPositionAndRotation(transform.position + Vector3.down * aimOffset, Quaternion.identity);
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;
            health -= amount;
            if (health <= 0f) Break();
        }

        void Break()
        {
            IsDestroyed = true;
            sensor.SetOnline(false);
            if (intactModel != null) intactModel.SetActive(false);
            if (brokenModel != null) brokenModel.SetActive(true);
            if (destroyFX != null) Instantiate(destroyFX, transform.position, Quaternion.identity);
            //Removing the target stops anyone still shooting at it
            if (hitbox != null) Destroy(hitbox.gameObject);

            var room = Room;
            Debug.Log("[ShipAI] Camera destroyed" + (room != null ? " in " + room.roomName : ""));
            if (ShipAI.Instance != null) ShipAI.Instance.RaiseAlarm(transform.position, room, "camera destroyed");
            Destroyed?.Invoke(this);
        }
    }
}
