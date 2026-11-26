using UnityEngine;

namespace Rambler.Crew.Battle
{
    //A ship on the tactical map: a light moving over the Titan surface, seen from above on the Weapons screen.
    //It turns and speeds up gradually toward whatever ShipBattle asks of it (Drive), so speed and heading take time
    //to change. Built by Tools > Company > Build Tactical Space.
    public class TacticalShip : MonoBehaviour
    {
        public enum Side { Player, Enemy }

        [SerializeField] Side side = Side.Enemy;
        [SerializeField] string displayName = "Ship";
        [Tooltip("Map units a second at full throttle.")]
        public float maxSpeed = 40f;
        [Tooltip("Degrees a second it can turn.")]
        [SerializeField] float turnRate = 45f;
        [Tooltip("Map units a second gained or lost each second.")]
        [SerializeField] float acceleration = 12f;
        [SerializeField] Light glow;
        [SerializeField] Renderer body;

        public Side Team => side;
        public string DisplayName { get => displayName; set => displayName = value; }
        public float Speed { get; private set; }
        public Vector3 Heading { get; private set; } = Vector3.forward;
        public Vector3 Position => transform.position;
        public Vector3 Velocity => Heading * Speed;
        public float SpeedShare => maxSpeed > 0f ? Speed / maxSpeed : 0f;

        void Awake()
        {
            Heading = Flat(transform.forward);
        }

        //Turn toward a direction and change speed toward a target speed, then move. A boost (an evasive burn) turns
        //and accelerates harder and can push a little past top speed.
        public void Drive(Vector3 direction, float targetSpeed, float dt, float boost = 1f)
        {
            direction = Flat(direction);
            if (direction.sqrMagnitude > 0.001f)
                Heading = Vector3.RotateTowards(Heading, direction, turnRate * boost * Mathf.Deg2Rad * dt, 0f);
            Speed = Mathf.MoveTowards(Speed, Mathf.Clamp(targetSpeed, 0f, maxSpeed * 1.5f), acceleration * boost * dt);
            transform.position += Heading * Speed * dt;
            transform.rotation = Quaternion.LookRotation(Heading, Vector3.up);
        }

        //Brighter when it's fast; dim when it's crippled
        public void SetGlow(float strength)
        {
            if (glow != null) glow.intensity = Mathf.Lerp(1.5f, 6f, Mathf.Clamp01(strength));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
    }
}
