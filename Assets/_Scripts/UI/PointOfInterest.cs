using System.Collections.Generic;
using UnityEngine;

namespace Rambler.UI
{
    //Marks something on the Command/Map screen: an objective, a terminal, an exit, loot. Exits (Portals), ship
    //terminals and crew terminals are marked automatically; add this to anything else worth showing.
    public class PointOfInterest : MonoBehaviour
    {
        public enum Kind { Objective, Terminal, Exit, Loot, Crew, Other }

        [SerializeField] Kind kind = Kind.Objective;
        [Tooltip("Shown under the marker. Empty = the object's name.")]
        [SerializeField] string label = "";
        [Tooltip("Hide it from the map without removing it (e.g. until it is discovered).")]
        [SerializeField] bool showOnMap = true;

        static readonly List<PointOfInterest> all = new List<PointOfInterest>();
        public static IReadOnlyList<PointOfInterest> All => all;

        public Kind Type => kind;
        public string Label => string.IsNullOrEmpty(label) ? name : label;
        public bool ShowOnMap { get => showOnMap; set => showOnMap = value; }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static string Glyph(Kind k)
        {
            switch (k)
            {
                case Kind.Objective: return "!";
                case Kind.Terminal: return "T";
                case Kind.Exit: return ">";
                case Kind.Loot: return "$";
                case Kind.Crew: return "C";
                default: return "?";
            }
        }

        public static Color Colour(Kind k)
        {
            switch (k)
            {
                case Kind.Objective: return new Color(1f, 0.82f, 0.2f);
                case Kind.Terminal: return new Color(0.3f, 0.75f, 1f);
                case Kind.Exit: return new Color(0.35f, 0.95f, 0.5f);
                case Kind.Loot: return new Color(0.95f, 0.85f, 0.45f);
                case Kind.Crew: return new Color(0.95f, 0.62f, 0.2f);
                default: return new Color(0.8f, 0.8f, 0.8f);
            }
        }
    }
}
