using System;
using System.Collections.Generic;
using UnityEngine;

namespace BadStar.Mine
{
    //Team colours for the Titan ore train. Every car shares one 256 px colour atlas (16 x 16 cells, one flat colour each).
    //This train gets its own copy of the atlas and of the two materials, so a livery change only repaints the two team
    //cells: cars keep their materials, and the cab roof's fade copies (made from the same texture) follow automatically.
    [DefaultExecutionOrder(-50)]   //Before HabitatCutaway.Start copies the roof materials
    public class TrainLivery : MonoBehaviour
    {
        public const int Cells = 16;
        public const int PrimaryCell = 0;     //Body panels, roof, hopper jackets
        public const int SecondaryCell = 1;   //Belts, nose panel, hopper rims, tank bands

        [Serializable]
        public struct Livery
        {
            public string name;
            public Color primary;
            public Color secondary;
        }

        [Tooltip("Shared atlas (Read/Write enabled, uncompressed)")] [SerializeField] Texture2D atlas;
        [SerializeField] Material atlasMaterial;
        [SerializeField] Material glowMaterial;
        [Tooltip("Every car of this train, including the cab roof")] [SerializeField] Renderer[] renderers;
        [SerializeField] Livery[] presets =
        {
            new Livery { name = "Titan Mining", primary = Hex("E9DFC4"), secondary = Hex("D2692A") },
            new Livery { name = "Cobalt",       primary = Hex("D9DEE6"), secondary = Hex("2F6FB5") },
            new Livery { name = "Crimson",      primary = Hex("E6DED6"), secondary = Hex("B33A3A") },
            new Livery { name = "Jade",         primary = Hex("DCE3D8"), secondary = Hex("2E8C6A") },
            new Livery { name = "Graphite",     primary = Hex("5B5E63"), secondary = Hex("E3B23C") },
        };
        [SerializeField] int startPreset;

        Texture2D texture;
        Material atlasInstance, glowInstance;
        Color32[] cell;

        public int PresetCount => presets.Length;
        public int CurrentPreset { get; private set; } = -1;
        public event Action<TrainLivery> Changed;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        void Awake()
        {
            if (atlas == null || atlasMaterial == null) { enabled = false; return; }
            texture = Instantiate(atlas);
            texture.name = atlas.name + " (" + name + ")";
            texture.filterMode = FilterMode.Point;
            atlasInstance = new Material(atlasMaterial) { name = atlasMaterial.name + " (" + name + ")" };
            atlasInstance.SetTexture("_BaseMap", texture);
            if (glowMaterial != null)
            {
                glowInstance = new Material(glowMaterial) { name = glowMaterial.name + " (" + name + ")" };
                glowInstance.SetTexture("_BaseMap", texture);
            }
            var swap = new Dictionary<Material, Material> { { atlasMaterial, atlasInstance } };
            if (glowInstance != null) swap[glowMaterial] = glowInstance;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && swap.TryGetValue(mats[i], out var m)) mats[i] = m;
                r.sharedMaterials = mats;
            }
            int px = texture.width / Cells;
            cell = new Color32[px * px];
            SetPreset(startPreset);
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (atlasInstance != null) Destroy(atlasInstance);
            if (glowInstance != null) Destroy(glowInstance);
        }

        public void SetPreset(int index)
        {
            if (presets.Length == 0) return;
            index = Mathf.Clamp(index, 0, presets.Length - 1);
            CurrentPreset = index;
            SetColours(presets[index].primary, presets[index].secondary);
        }

        public void SetColours(Color primary, Color secondary)
        {
            if (texture == null) return;
            Paint(PrimaryCell, primary);
            Paint(SecondaryCell, secondary);
            texture.Apply(false);
            Changed?.Invoke(this);
        }

        void Paint(int index, Color colour)
        {
            int px = texture.width / Cells;
            Color32 c = colour;
            for (int i = 0; i < cell.Length; i++) cell[i] = c;
            //Texture rows run bottom-up; atlas cell rows are counted from the top
            texture.SetPixels32((index % Cells) * px, texture.height - (index / Cells + 1) * px, px, px, cell);
        }
    }
}
