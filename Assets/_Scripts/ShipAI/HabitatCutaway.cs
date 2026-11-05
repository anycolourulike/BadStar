using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

namespace Rambler.Ship
{
    //Fades a building's roofs (and anything stacked on them) out while the active player is inside
    //one of its rooms, or walking up to an external door, so the top-down camera can see in.
    //Fades them back in on the way out.
    public class HabitatCutaway : MonoBehaviour
    {
        [SerializeField] ShipRoom[] rooms;
        [Tooltip("Trigger boxes outside the external doors; the roof also fades while the player is in one, so they can see the way in.")]
        [SerializeField] Collider[] approachZones;
        [Tooltip("Faded out while the player is inside.")]
        [SerializeField] Renderer[] roofs;
        [Tooltip("Transparent URP Lit material; each roof material is copied onto it while fading. Without one the roofs just switch off.")]
        [SerializeField] Material fadeTemplate;
        [SerializeField] float fadeDuration = 0.4f;
        [Tooltip("How long the player must be outside every room before the roof comes back (bridges doorways between rooms).")]
        [SerializeField] float exitDelay = 0.4f;
        [SerializeField] float checkInterval = 0.1f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        PlayerSelector playerSelector;
        Material[][] opaqueMaterials;
        Material[][] fadeMaterials;
        readonly List<Material> fadeCopies = new();
        float alpha = 1f;
        float lastInside = -999f;
        float timer;

        void Start()
        {
            var playerCore = GameObject.Find("/PlayerCore");
            if (playerCore != null) playerSelector = playerCore.GetComponent<PlayerSelector>();
            if (fadeTemplate != null) BuildFadeMaterials();
        }

        //One transparent copy per roof material, shared between roofs that use the same material
        void BuildFadeMaterials()
        {
            var copies = new Dictionary<Material, Material>();
            opaqueMaterials = new Material[roofs.Length][];
            fadeMaterials = new Material[roofs.Length][];
            for (int i = 0; i < roofs.Length; i++)
            {
                if (roofs[i] == null) continue;
                opaqueMaterials[i] = roofs[i].sharedMaterials;
                fadeMaterials[i] = new Material[opaqueMaterials[i].Length];
                for (int k = 0; k < opaqueMaterials[i].Length; k++)
                {
                    var source = opaqueMaterials[i][k];
                    if (source == null) continue;
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = new Material(fadeTemplate) { name = source.name + " (Fade)" };
                        if (source.HasProperty(BaseColor)) copy.SetColor(BaseColor, source.GetColor(BaseColor));
                        if (source.HasProperty(BaseMap)) copy.SetTexture(BaseMap, source.GetTexture(BaseMap));
                        if (source.HasProperty(Metallic)) copy.SetFloat(Metallic, source.GetFloat(Metallic));
                        if (source.HasProperty(Smoothness)) copy.SetFloat(Smoothness, source.GetFloat(Smoothness));
                        copies[source] = copy;
                        fadeCopies.Add(copy);
                    }
                    fadeMaterials[i][k] = copy;
                }
            }
        }

        void OnDestroy()
        {
            foreach (var copy in fadeCopies) Destroy(copy);
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= checkInterval)
            {
                timer = 0f;
                var player = playerSelector != null ? playerSelector.activePlayer : null;
                if (player != null && IsInside(player.transform.position + Vector3.up)) lastInside = Time.time;
            }

            bool hide = Time.time - lastInside < exitDelay;
            float target = hide ? 0f : 1f;
            if (Mathf.Approximately(alpha, target)) return;
            alpha = fadeTemplate != null && fadeDuration > 0f ? Mathf.MoveTowards(alpha, target, Time.deltaTime / fadeDuration) : target;
            ApplyAlpha();
        }

        void ApplyAlpha()
        {
            for (int i = 0; i < roofs.Length; i++)
            {
                var roof = roofs[i];
                if (roof == null) continue;
                roof.enabled = alpha > 0f;
                if (fadeMaterials == null || fadeMaterials[i] == null) continue;
                //Back on the opaque originals once fully shown, so the roof sorts and casts shadows normally
                roof.sharedMaterials = alpha >= 1f ? opaqueMaterials[i] : fadeMaterials[i];
            }
            foreach (var copy in fadeCopies)
            {
                var c = copy.GetColor(BaseColor);
                c.a = alpha;
                copy.SetColor(BaseColor, c);
            }
        }

        bool IsInside(Vector3 position)
        {
            foreach (var room in rooms)
            {
                if (room != null && room.Contains(position)) return true;
            }
            if (approachZones == null) return false;
            foreach (var zone in approachZones)
            {
                //ClosestPoint returns the point itself when it is inside
                if (zone != null && zone.enabled && (zone.ClosestPoint(position) - position).sqrMagnitude < 0.0001f) return true;
            }
            return false;
        }
    }
}
