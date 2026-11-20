using UnityEngine;

namespace Rambler.Mission
{
    //A light on the building's grid: full on the mains, dark in a blackout, a dim red on the backup generators.
    [RequireComponent(typeof(Light))]
    public class PoweredLight : MonoBehaviour
    {
        [SerializeField] float blackoutIntensity = 0f;
        [SerializeField] float backupIntensityScale = 0.45f;
        [SerializeField] Color backupColor = new Color(1f, 0.25f, 0.15f);

        Light lamp;
        float baseIntensity;
        Color baseColor;
        PowerGrid grid;

        void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            baseColor = lamp.color;
        }

        void Start()
        {
            grid = PowerGrid.Instance;
            if (grid != null) { grid.Changed += Apply; Apply(grid.Current); }
        }

        void OnDestroy() { if (grid != null) grid.Changed -= Apply; }

        void Apply(PowerGrid.State s)
        {
            switch (s)
            {
                case PowerGrid.State.Mains: lamp.intensity = baseIntensity; lamp.color = baseColor; break;
                case PowerGrid.State.Blackout: lamp.intensity = blackoutIntensity; break;
                case PowerGrid.State.Backup: lamp.intensity = baseIntensity * backupIntensityScale; lamp.color = backupColor; break;
            }
        }
    }
}
