using System.Collections;
using UnityEngine;

namespace Rambler.Mine
{
    //The travelling load-out gantry at the Fastnet ore stand. When the freighter is down and empty it starts the
    //conveyor, drives over each hopper in turn, lowers the telescopic chute, pours (the hopper's heap grows), raises
    //the chute, then parks at the north end of the rails (clear of the freighter's vertical take-off) and sends it off.
    public class OreLoadout : MonoBehaviour
    {
        [SerializeField] FreighterFlight freighter;
        [SerializeField] Transform gantry;
        [SerializeField] Transform chute;
        [SerializeField] OreConveyorFlow conveyor;
        [SerializeField] Material oreMaterial;

        [Header("Gantry")]
        [SerializeField] float gantrySpeed = 2.0f;
        [Tooltip("Parking spot along the rails (world Z), clear of the freighter")]
        [SerializeField] float parkZ = 241.0f;
        [Header("Chute (model position is the extended pour position)")]
        [SerializeField] float chuteLift = 2.0f;
        [SerializeField] float chuteTime = 1.5f;
        [SerializeField] float chuteBottomBelowOrigin = 3.35f;   //From the chute origin down to the rubber skirt
        [Header("Loading")]
        [SerializeField] float fillTime = 7f;
        [SerializeField] float startDelay = 4f;

        Vector3 chuteDown;      //Extended (model) local position
        Transform stream;

        public bool Busy { get; private set; }

        void Start()
        {
            if (freighter == null) freighter = FindFirstObjectByType<FreighterFlight>();
            if (gantry == null || chute == null || freighter == null) { enabled = false; return; }
            chuteDown = chute.localPosition;
            SetChute(0f);
            var p = gantry.position; p.z = parkZ; gantry.position = p;
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(s.GetComponent<Collider>());
            s.name = "OreStream"; stream = s.transform; stream.SetParent(transform, false);
            if (oreMaterial != null) s.GetComponent<Renderer>().sharedMaterial = oreMaterial;
            s.SetActive(false);
            StartCoroutine(Loop());
        }

        //0 = raised clear of the catwalks, 1 = extended into the hopper
        void SetChute(float t)
        {
            var up = chute.parent != null ? chute.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            chute.localPosition = chuteDown + up * chuteLift * (1f - t);
        }

        IEnumerator MoveGantryTo(float z)
        {
            while (Mathf.Abs(gantry.position.z - z) > 0.01f)
            {
                var p = gantry.position; p.z = Mathf.MoveTowards(p.z, z, gantrySpeed * Time.deltaTime); gantry.position = p;
                yield return null;
            }
        }

        IEnumerator Over(float time, System.Action<float> step)
        {
            for (float t = 0f; t < time; t += Time.deltaTime) { step(t / time); yield return null; }
            step(1f);
        }

        void ShowStream(Vector3 from, Vector3 to, bool on)
        {
            stream.gameObject.SetActive(on);
            if (!on) return;
            float len = Mathf.Max(0.1f, from.y - to.y), wobble = 1f + 0.12f * Mathf.Sin(Time.time * 23f);
            stream.position = (from + to) * 0.5f; stream.rotation = Quaternion.identity;
            stream.localScale = new Vector3(0.7f * wobble, len * 0.5f, 0.7f * wobble);
        }

        IEnumerator Loop()
        {
            while (true)
            {
                while (freighter == null || !freighter.ReadyToLoad) yield return new WaitForSeconds(1f);
                yield return new WaitForSeconds(startDelay);
                Busy = true;
                if (conveyor != null) conveyor.SetRunning(true);
                for (int i = 0; i < freighter.HopperCount; i++)
                {
                    var top = freighter.HopperTop(i);
                    yield return MoveGantryTo(top.z);
                    yield return Over(chuteTime, t => SetChute(t));
                    int hop = i;
                    yield return Over(fillTime, t =>
                    {
                        freighter.SetOre(hop, t);
                        var bottom = chute.position + Vector3.down * chuteBottomBelowOrigin;
                        ShowStream(bottom, new Vector3(bottom.x, top.y - 2.5f + 3.2f * t, bottom.z), t < 0.97f);
                    });
                    ShowStream(Vector3.zero, Vector3.zero, false);
                    yield return Over(chuteTime, t => SetChute(1f - t));
                }
                if (conveyor != null) conveyor.SetRunning(false);
                yield return MoveGantryTo(parkZ);
                Busy = false;
                freighter.Depart();
                while (freighter.Current != FreighterFlight.State.Landed || freighter.Loaded) yield return new WaitForSeconds(1f);
            }
        }
    }
}
