using UnityEngine;
using Rambler.Attributes;
using Rambler.Control;

namespace Rambler.UI
{
    //A small ring on the ground under the character the player is controlling. It moves to whoever
    //PlayerSelector switches to (hired crew included) and hides while that character is dead.
    //Makes itself the first time a player is chosen; place one in a scene to change its look there.
    public class ActivePlayerMarker : MonoBehaviour
    {
        [SerializeField] Color color = new Color(0.25f, 1f, 0.75f, 0.85f);
        [Tooltip("Ring radius in metres.")]
        [SerializeField] float radius = 0.45f;
        [Tooltip("Ring thickness as a fraction of the radius.")]
        [Range(0.05f, 0.5f)] [SerializeField] float thickness = 0.16f;
        [Tooltip("Height above the character's feet, to stay clear of the floor.")]
        [SerializeField] float lift = 0.03f;
        [Tooltip("Gentle size pulse (fraction of the radius); 0 = still.")]
        [SerializeField] float pulse = 0.06f;
        [SerializeField] float pulseSpeed = 3f;

        static ActivePlayerMarker instance;

        Transform target;
        Health targetHealth;
        SpriteRenderer ring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            instance = null;
            PlayerSelector.OnPlayerObjectChanged -= OnPlayerChanged;
            PlayerSelector.OnPlayerObjectChanged += OnPlayerChanged;
        }

        static void OnPlayerChanged(GameObject player)
        {
            if (instance == null) instance = FindObjectOfType<ActivePlayerMarker>();
            if (instance == null) instance = new GameObject("ActivePlayerMarker").AddComponent<ActivePlayerMarker>();
            instance.Follow(player);
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            var child = new GameObject("Ring");
            child.transform.SetParent(transform, false);
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); //Lie flat, facing up
            ring = child.AddComponent<SpriteRenderer>();
            ring.sprite = MakeRing(thickness);
            ring.color = color;
            ring.sortingOrder = 10;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void Follow(GameObject player)
        {
            target = player != null ? player.transform : null;
            targetHealth = player != null ? player.GetComponent<Health>() : null;
            LateUpdate();
        }

        void LateUpdate()
        {
            bool show = target != null && (targetHealth == null || !targetHealth.IsDead());
            if (ring.enabled != show) ring.enabled = show;
            if (!show) return;
            transform.position = target.position + Vector3.up * lift;
            float size = radius * 2f * (1f + pulse * Mathf.Sin(Time.time * pulseSpeed));
            ring.transform.localScale = new Vector3(size, size, 1f);
        }

        //A soft-edged white ring, one unit across, tinted by the renderer's colour
        static Sprite MakeRing(float thickness)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float outer = size * 0.5f - 1f, inner = outer * (1f - thickness), edge = 1.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float a = Mathf.Clamp01((outer - d) / edge) * Mathf.Clamp01((d - inner) / edge);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
