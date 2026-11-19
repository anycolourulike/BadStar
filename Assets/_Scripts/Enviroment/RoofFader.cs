using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rambler
{
    //Put on a roof. Fades it out while a character stands in any of its RoofFadeZones (doorways,
    //windows, the room inside) and back in when they've all left. Shadows can stay on so the
    //inside keeps its shade while the roof is see-through.
    public class RoofFader : MonoBehaviour
    {
        [Tooltip("Roof opacity while someone is near or inside")]
        [Range(0f, 1f)] [SerializeField] float fadedAlpha = 0.15f;
        [Tooltip("Seconds to fade fully out or in")]
        [SerializeField] float fadeTime = 0.35f;
        [Tooltip("Keep casting the roof's shadow while faded")]
        [SerializeField] bool keepShadow = true;
        [Tooltip("Optional see-through material to fade with. Leave empty to make one from each roof material.")]
        [SerializeField] Material fadeMaterialOverride;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color = Shader.PropertyToID("_Color");

        //How many zones each character is in: walking from a doorway into the room overlaps two
        readonly Dictionary<Collider, int> occupants = new Dictionary<Collider, int>();
        readonly List<Collider> gone = new List<Collider>();
        Renderer[] renderers;
        Material[][] opaqueMats;
        Material[][] fadeMats;
        ShadowCastingMode[] shadowModes;
        float alpha = 1f;
        bool usingFade;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            opaqueMats = new Material[renderers.Length][];
            fadeMats = new Material[renderers.Length][];
            shadowModes = new ShadowCastingMode[renderers.Length];
            for (int r = 0; r < renderers.Length; r++)
            {
                opaqueMats[r] = renderers[r].sharedMaterials;
                shadowModes[r] = renderers[r].shadowCastingMode;
                fadeMats[r] = new Material[opaqueMats[r].Length];
                for (int m = 0; m < opaqueMats[r].Length; m++)
                    fadeMats[r][m] = MakeFadeMaterial(opaqueMats[r][m]);
            }
        }

        void OnDestroy()
        {
            if (fadeMats == null) return;
            foreach (var mats in fadeMats)
                foreach (var m in mats)
                    if (m != null) Destroy(m);
        }

        public void Enter(Collider who)
        {
            occupants.TryGetValue(who, out int n);
            occupants[who] = n + 1;
        }

        public void Exit(Collider who)
        {
            if (!occupants.TryGetValue(who, out int n)) return;
            if (n <= 1) occupants.Remove(who);
            else occupants[who] = n - 1;
        }

        void Update()
        {
            //Characters that died, were destroyed or switched off inside a zone no longer count
            gone.Clear();
            foreach (var c in occupants.Keys)
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) gone.Add(c);
            foreach (var c in gone) occupants.Remove(c);

            float target = occupants.Count > 0 ? fadedAlpha : 1f;
            if (Mathf.Approximately(alpha, target)) return;
            float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
            alpha = Mathf.MoveTowards(alpha, target, step);
            Apply();
        }

        void Apply()
        {
            bool needFade = alpha < 0.999f;
            if (needFade != usingFade)
            {
                usingFade = needFade;
                for (int r = 0; r < renderers.Length; r++)
                {
                    renderers[r].sharedMaterials = usingFade ? fadeMats[r] : opaqueMats[r];
                    renderers[r].shadowCastingMode = shadowModes[r];
                }
            }
            if (!usingFade) return;

            for (int r = 0; r < renderers.Length; r++)
            {
                foreach (var m in fadeMats[r]) SetAlpha(m, alpha);
                //A shader that can't go see-through just hides; its shadow can stay behind
                bool canFade = SupportsFade(fadeMats[r]);
                if (!canFade)
                {
                    renderers[r].shadowCastingMode = alpha < 0.5f
                        ? (keepShadow ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.Off)
                        : shadowModes[r];
                }
                else if (!keepShadow)
                {
                    renderers[r].shadowCastingMode = alpha < 0.5f ? ShadowCastingMode.Off : shadowModes[r];
                }
            }
        }

        Material MakeFadeMaterial(Material source)
        {
            if (source == null) return null;
            var m = new Material(fadeMaterialOverride != null ? fadeMaterialOverride : source);
            m.name = source.name + " (Fade)";
            if (fadeMaterialOverride != null) return m;

            //URP Lit, and other URP shaders using its property names (FlatKit's Stylized Surface does)
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); //Transparent
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f); //Alpha
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
            }
            return m;
        }

        static bool SupportsFade(Material[] mats)
        {
            foreach (var m in mats)
                if (m != null && m.renderQueue < (int)RenderQueue.Transparent) return false;
            return true;
        }

        static void SetAlpha(Material m, float a)
        {
            if (m == null) return;
            int id = m.HasProperty(BaseColor) ? BaseColor : m.HasProperty(Color) ? Color : -1;
            if (id < 0) return;
            var c = m.GetColor(id);
            c.a = a;
            m.SetColor(id, c);
        }
    }
}
