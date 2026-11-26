using UnityEngine;

namespace Rambler.Crew.Battle
{
    //The flash of a missile going off on the tactical map: a ball that swells and shrinks with a burst of light.
    public class TacticalBlast : MonoBehaviour
    {
        [SerializeField] float size = 18f;
        [SerializeField] float seconds = 0.6f;
        [SerializeField] Light flash;

        float t;
        float lightStart;

        void Start()
        {
            transform.rotation = Quaternion.identity;
            if (flash != null) lightStart = flash.intensity;
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            t += Time.deltaTime / Mathf.Max(0.05f, seconds);
            float s = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            transform.localScale = Vector3.one * size * s;
            if (flash != null) flash.intensity = lightStart * (1f - Mathf.Clamp01(t));
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
