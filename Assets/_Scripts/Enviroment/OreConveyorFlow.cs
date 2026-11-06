using UnityEngine;

namespace Rambler.Mine
{
    //Ore lumps riding the overland conveyor from silo 2 to the load-out gantry's bin while the loadout runs. The path's
    //last two legs follow the gantry: along the rail-side shuttle belt to the tripper, then across the bridge to the bin.
    public class OreConveyorFlow : MonoBehaviour
    {
        [Tooltip("Fixed belt route, world space, silo 2 first")]
        [SerializeField] Vector3[] route =
        {
            new Vector3(75.5f, 15.15f, 88f), new Vector3(92f, 15.15f, 88f), new Vector3(92f, 15.15f, 198f), new Vector3(84f, 15.15f, 198f)
        };
        [SerializeField] Transform gantry;                     //Moving: the shuttle belt ends at its tripper
        [SerializeField] Vector3 tripperOffset = new Vector3(18f, 0.6f, 0f);   //From the gantry origin (x 66) to the tripper (x 84)
        [SerializeField] Vector3 binOffset = new Vector3(0f, 12.4f, 0f);       //From the gantry origin to the bin top
        [SerializeField] Material oreMaterial;
        [SerializeField] int lumps = 70;
        [SerializeField] float speed = 2.5f;
        [SerializeField] float lumpSize = 0.38f;

        Transform[] pool;
        float offset;
        bool running, started;
        Vector3[] pts = new Vector3[0];
        float[] cum = new float[0];

        public void SetRunning(bool on)
        {
            running = on;
            if (on && !started) { started = true; foreach (var t in pool) t.gameObject.SetActive(true); }
        }

        void Awake()
        {
            pool = new Transform[lumps];
            for (int i = 0; i < lumps; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>());
                g.name = "OreLump"; g.transform.SetParent(transform, false);
                g.transform.localScale = Vector3.one * lumpSize * Random.Range(0.7f, 1.3f);
                g.transform.rotation = Random.rotation;
                if (oreMaterial != null) g.GetComponent<Renderer>().sharedMaterial = oreMaterial;
                g.SetActive(false); pool[i] = g.transform;
            }
        }

        void Rebuild()
        {
            int n = route.Length + (gantry != null ? 2 : 0);
            if (pts.Length != n) { pts = new Vector3[n]; cum = new float[n]; }
            for (int i = 0; i < route.Length; i++) pts[i] = route[i];
            if (gantry != null)
            {
                var g = gantry.position;
                pts[route.Length] = new Vector3(route[route.Length - 1].x, route[route.Length - 1].y, g.z);     //Shuttle belt to the tripper
                pts[route.Length + 1] = new Vector3(g.x, g.y + binOffset.y, g.z);                             //Across the bridge to the bin
            }
            cum[0] = 0f;
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        }

        Vector3 At(float d)
        {
            for (int i = 1; i < pts.Length; i++)
                if (d <= cum[i]) return Vector3.Lerp(pts[i - 1], pts[i], (d - cum[i - 1]) / Mathf.Max(0.001f, cum[i] - cum[i - 1]));
            return pts[pts.Length - 1];
        }

        void Update()
        {
            if (!started) return;
            Rebuild();
            float total = cum[cum.Length - 1];
            if (running) offset = (offset + speed * Time.deltaTime) % total;
            for (int i = 0; i < pool.Length; i++)
            {
                float d = (offset + total * i / pool.Length) % total;
                pool[i].position = At(d);
            }
        }
    }
}
