using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Combat
{
    //A spot next to cover. A character that stops within its radius takes cover here (see CoverUser).
    //Forward points from the character towards the cover, i.e. towards where the danger is expected.
    //Low cover: crouch behind furniture. High cover: stand beside a door frame or wall edge and lean out to one side.
    public class CoverPoint : MonoBehaviour
    {
        public enum CoverType { Low, High }
        public enum CoverSide { None, Left, Right }

        static readonly List<CoverPoint> all = new();
        public static IReadOnlyList<CoverPoint> All => all;

        [SerializeField] CoverType type = CoverType.Low;
        [Tooltip("High cover only: which side the character leans out from.")]
        [SerializeField] CoverSide side = CoverSide.None;
        [Tooltip("A character that stops within this distance takes this cover.")]
        [SerializeField] float radius = 0.9f;
        [Tooltip("Share of damage still taken while using this cover (0.5 = half damage).")]
        [Range(0f, 1f)] [SerializeField] float damageTaken = 0.5f;

        public CoverType Type => type;
        public CoverSide Side => side;
        public float Radius => radius;
        public float DamageTaken => damageTaken;
        public Vector3 Facing => transform.forward;
        public GameObject Occupant { get; private set; }
        public bool IsFree => Occupant == null;

        void OnEnable() => all.Add(this);
        void OnDisable()
        {
            all.Remove(this);
            Occupant = null;
        }

        public void Setup(CoverType coverType, CoverSide coverSide, float zoneRadius)
        {
            type = coverType;
            side = coverSide;
            radius = zoneRadius;
        }

        public bool TryOccupy(GameObject who)
        {
            if (Occupant != null && Occupant != who) return false;
            Occupant = who;
            return true;
        }

        public void Release(GameObject who)
        {
            if (Occupant == who) Occupant = null;
        }

        //Closest free point whose zone contains the position
        public static CoverPoint FindAt(Vector3 position, GameObject who)
        {
            CoverPoint best = null;
            float bestDist = float.MaxValue;
            foreach (var point in all)
            {
                if (!point.IsFree && point.Occupant != who) continue;
                Vector3 offset = point.transform.position - position;
                offset.y = 0f;
                float dist = offset.magnitude;
                if (dist <= point.radius && dist < bestDist && Mathf.Abs(point.transform.position.y - position.y) < 1f)
                {
                    best = point;
                    bestDist = dist;
                }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            bool low = type == CoverType.Low;
            Color c = !Application.isPlaying || IsFree ? (low ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.3f, 0.6f, 1f)) : new Color(1f, 0.6f, 0.1f);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.6f);
            Vector3 p = transform.position;
            float h = low ? 0.9f : 1.8f;
            Gizmos.DrawWireCube(p + Vector3.up * h / 2f, new Vector3(0.35f, h, 0.35f));
            Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.up * 0.1f + transform.forward * 0.7f);
            if (!low && side != CoverSide.None)
            {
                Vector3 lean = side == CoverSide.Left ? -transform.right : transform.right;
                Gizmos.DrawLine(p + Vector3.up * 1.5f, p + Vector3.up * 1.5f + lean * 0.5f);
            }
            Gizmos.color = new Color(c.r, c.g, c.b, 0.15f);
            const int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Gizmos.DrawLine(p + new Vector3(Mathf.Cos(a0), 0.02f, Mathf.Sin(a0)) * radius, p + new Vector3(Mathf.Cos(a1), 0.02f, Mathf.Sin(a1)) * radius);
            }
        }
    }
}
