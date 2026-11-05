using UnityEngine;

namespace Rambler.Mine
{
    //Guard tower searchlight: sweeps slowly left and right about its starting heading
    public class Searchlight : MonoBehaviour
    {
        [SerializeField] float sweepAngle = 50f;
        [SerializeField] float period = 9f;
        [SerializeField] float phase;

        Quaternion baseRotation;

        void Start() => baseRotation = transform.localRotation;

        void Update()
        {
            float a = Mathf.Sin((Time.time / Mathf.Max(0.1f, period) + phase) * Mathf.PI * 2f) * sweepAngle;
            transform.localRotation = Quaternion.AngleAxis(a, Vector3.up) * baseRotation;
        }
    }
}
