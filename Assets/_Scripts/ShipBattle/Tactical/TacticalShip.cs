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

        // ---------- Shield bubble ----------

        [Header("Shield bubble")]
        [Tooltip("Radius of the first shield ring (map units); each further layer is a wider ring.")]
        [SerializeField] float shieldRadius = 40f;
        [SerializeField] float layerSpacing = 13f;
        [SerializeField] Color shieldColour = new Color(0.45f, 0.8f, 1f, 1f);
        [SerializeField] Color enemyShieldColour = new Color(1f, 0.45f, 0.85f, 1f);

        static Sprite ringSprite;
        static Material ringMaterial;
        readonly SpriteRenderer[] rings = new SpriteRenderer[2];
        float flash;

        //Rings around the light, one per layer up; the next layer charging shows faintly
        public void ShowShield(int layers, float charge)
        {
            if (flash > 0f) flash -= Time.deltaTime;
            for (int i = 0; i < rings.Length; i++)
            {
                float fill = Mathf.Clamp01(charge - i); //1 = up, between = charging
                if (fill <= 0.01f) { if (rings[i] != null) rings[i].enabled = false; continue; }
                var r = Ring(i);
                r.enabled = true;
                var c = side == Side.Player ? shieldColour : enemyShieldColour;
                float alpha = i < layers ? 0.85f : 0.12f + 0.25f * fill;
                if (flash > 0f) alpha = Mathf.Min(1f, alpha + flash * 2f);
                r.color = new Color(c.r, c.g, c.b, alpha);
                float size = (shieldRadius + i * layerSpacing) * 2f * (1f + Mathf.Max(0f, flash) * 0.4f);
                //In map units, whatever the ship's own scale
                float parent = Mathf.Max(0.001f, transform.lossyScale.x);
                r.transform.localScale = new Vector3(size / parent, size / parent, 1f);
            }
        }

        //A layer just took a hit
        public void FlashShield() => flash = 0.35f;

        SpriteRenderer Ring(int i)
        {
            if (rings[i] != null) return rings[i];
            var go = new GameObject("ShieldRing" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); //Lying flat, seen from above
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RingSprite();
            if (ringMaterial == null) { var sh = Shader.Find("Sprites/Default"); if (sh != null) ringMaterial = new Material(sh); }
            if (ringMaterial != null) sr.sharedMaterial = ringMaterial;
            rings[i] = sr;
            return sr;
        }

        //A soft white ring, one unit across, made once
        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ShieldRing" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.07f); //The ring
                    float inner = d < 0.9f ? 0.12f * d : 0f;                       //A faint fill inside
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(edge + inner));
                }
            tex.SetPixels32(px);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return ringSprite;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
    }
}
