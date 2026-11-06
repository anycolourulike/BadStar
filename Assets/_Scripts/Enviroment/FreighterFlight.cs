using System.Collections;
using UnityEngine;

namespace Rambler.Mine
{
    //An ore freighter (the Fastnet) working a timed loop from its stand: it waits while it is loaded, then spools up,
    //lifts straight off the stand, tilts its fans forward and flies out past the fog, waits away (unloading), then flies
    //back, descends onto the stand and spools down, empty, ready for the next load. OreLoadout fills the hoppers and
    //calls Depart() when it is done.
    public class FreighterFlight : MonoBehaviour
    {
        public enum State { Landed, Spooling, Lifting, Departing, Away, Arriving, Descending, SpoolingDown }

        [Header("Moving parts (from the model)")]
        [SerializeField] Transform[] fans;          //Tilt about their local X
        [SerializeField] Transform[] rotors;        //Spin about their local Z
        [SerializeField] Transform[] oreHeaps;      //Grow along their local Z (pivot at the bin floor)
        [Tooltip("Hopper centres along the hull, in model metres (same order as the heaps)")]
        [SerializeField] float[] hopperY = { -10.8f, -3.9f, 2.9f };
        [SerializeField] float hopperRimZ = 7.0f;

        [Header("Flight")]
        [SerializeField] float hoverHeight = 40f;
        [SerializeField] float cruiseDistance = 420f;
        [Tooltip("Compass heading it leaves and returns on (degrees from north, +Z)")]
        [SerializeField] float heading = 30f;
        [SerializeField] float cruiseClimb = 25f;
        [SerializeField] float cruiseTilt = -90f;
        [SerializeField] float spoolTime = 3f, liftTime = 9f, tiltTime = 4f, cruiseTime = 16f, awayTime = 40f;
        [SerializeField] float rotorMaxSpeed = 900f;   //Degrees per second at full power

        public State Current { get; private set; } = State.Landed;
        public bool Loaded { get; private set; }
        public bool ReadyToLoad => Current == State.Landed && !Loaded;

        Vector3 padPos;
        Quaternion padRot;
        Quaternion[] fanBase, rotorBase;
        Vector3[] heapBase;
        float power;        //0 rotors stopped .. 1 full speed
        float[] spin;

        void Awake()
        {
            padPos = transform.position; padRot = transform.rotation;
            fanBase = new Quaternion[fans.Length]; for (int i = 0; i < fans.Length; i++) fanBase[i] = fans[i].localRotation;
            rotorBase = new Quaternion[rotors.Length]; for (int i = 0; i < rotors.Length; i++) rotorBase[i] = rotors[i].localRotation;
            spin = new float[rotors.Length]; for (int i = 0; i < spin.Length; i++) spin[i] = i * 37f;
            heapBase = new Vector3[oreHeaps.Length]; for (int i = 0; i < oreHeaps.Length; i++) heapBase[i] = oreHeaps[i].localScale;
            for (int i = 0; i < oreHeaps.Length; i++) SetOre(i, 0f);    //Arrives empty
        }

        //Where ore should land for hopper i (top centre of the bin, world space)
        public Vector3 HopperTop(int i) => transform.TransformPoint(new Vector3(0f, hopperY[i], -hopperRimZ));
        public int HopperCount => Mathf.Min(hopperY.Length, oreHeaps.Length);

        public void SetOre(int i, float fill)
        {
            if (i < 0 || i >= oreHeaps.Length || oreHeaps[i] == null) return;
            fill = Mathf.Clamp01(fill);
            var s = heapBase[i]; s.z *= Mathf.Max(0.02f, fill);
            oreHeaps[i].localScale = s;
            oreHeaps[i].gameObject.SetActive(fill > 0.01f);
        }

        public void Depart()
        {
            if (Current != State.Landed) return;
            Loaded = true;
            StartCoroutine(Flight());
        }

        void SetTilt(float deg)
        {
            for (int i = 0; i < fans.Length; i++) fans[i].localRotation = fanBase[i] * Quaternion.AngleAxis(deg, Vector3.right);
        }

        void Update()
        {
            for (int i = 0; i < rotors.Length; i++)
            {
                spin[i] = (spin[i] + rotorMaxSpeed * power * Time.deltaTime * (i % 2 == 0 ? 1f : -1f)) % 360f;
                rotors[i].localRotation = rotorBase[i] * Quaternion.AngleAxis(spin[i], Vector3.forward);
            }
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        IEnumerator Over(float time, System.Action<float> step)
        {
            for (float t = 0f; t < time; t += Time.deltaTime) { step(Ease(t / time)); yield return null; }
            step(1f);
        }

        IEnumerator Flight()
        {
            var yawRot = Quaternion.Euler(0f, heading, 0f);
            Vector3 hover = padPos + Vector3.up * hoverHeight;
            Vector3 far = hover + yawRot * Vector3.forward * cruiseDistance + Vector3.up * cruiseClimb;

            Current = State.Spooling;
            yield return Over(spoolTime, t => power = t);

            Current = State.Lifting;                                     //Straight up, turning onto the heading
            yield return Over(liftTime, t => { transform.position = Vector3.Lerp(padPos, hover, t); transform.rotation = Quaternion.Slerp(padRot, yawRot * padRot, t); });

            Current = State.Departing;                                   //Fans tilt forward as it accelerates away
            yield return Over(cruiseTime, t => { transform.position = Vector3.Lerp(hover, far, t * t); SetTilt(cruiseTilt * Mathf.Clamp01(t * cruiseTime / tiltTime)); });

            Current = State.Away;                                        //Out of sight: unload
            for (int i = 0; i < oreHeaps.Length; i++) SetOre(i, 0f);
            Loaded = false;
            yield return new WaitForSeconds(awayTime);

            Current = State.Arriving;                                    //Back in, fans tilting up for the hover
            yield return Over(cruiseTime, t => { transform.position = Vector3.Lerp(far, hover, 1f - (1f - t) * (1f - t)); SetTilt(cruiseTilt * Mathf.Clamp01((1f - t) * cruiseTime / tiltTime)); });

            Current = State.Descending;
            yield return Over(liftTime, t => { transform.position = Vector3.Lerp(hover, padPos, t); transform.rotation = Quaternion.Slerp(yawRot * padRot, padRot, t); });
            transform.SetPositionAndRotation(padPos, padRot); SetTilt(0f);

            Current = State.SpoolingDown;
            yield return Over(spoolTime, t => power = 1f - t);
            Current = State.Landed;
        }
    }
}
