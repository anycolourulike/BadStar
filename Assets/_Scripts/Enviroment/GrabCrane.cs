using System.Collections;
using UnityEngine;

namespace Rambler.Mine
{
    //Rail-mounted gantry grab crane at the ore yard. When the ore train stops at the yard it holds the train, travels
    //to each loaded ore car in turn, grabs the load, swings it over the stockpile and drops it, then releases the train.
    //The loads are shown again when the train stops at the underground loading station.
    public class GrabCrane : MonoBehaviour
    {
        [SerializeField] TrainLoop train;
        [SerializeField] string unloadStation = "YARD";
        [SerializeField] string loadStation = "STATION";
        [Header("Moving parts (gantry carries the trolley, the trolley carries the grab)")]
        [SerializeField] Transform gantry;
        [SerializeField] Transform trolley;
        [SerializeField] Transform grab;
        [Tooltip("Hoist cables: unit length mesh, stretched between the trolley and the grab")] [SerializeField] Transform cable;
        [Tooltip("Ore lump shown in the grab while it carries a load")] [SerializeField] GameObject grabOre;
        [Header("Ore cars and their loads (same order)")]
        [SerializeField] Transform[] oreCars;
        [SerializeField] GameObject[] oreLoads;
        [Tooltip("The train and its cars live in the always-loaded core scene while the crane streams in with the yard, " +
                 "so they are found again by these names when the yard loads")]
        [SerializeField] string[] oreCarNames;
        [SerializeField] string[] oreLoadNames;
        [Header("Heights and positions (world)")]
        [SerializeField] float pileX;
        [SerializeField] float pileDropY;
        [SerializeField] float carGrabY;
        [SerializeField] float travelY;
        [SerializeField] float gantryMinZ = -100f, gantryMaxZ = 100f;
        [Header("Speeds")]
        [SerializeField] float gantrySpeed = 1.6f;
        [SerializeField] float trolleySpeed = 2.2f;
        [SerializeField] float hoistSpeed = 2.4f;
        [SerializeField] float grabPause = 0.6f;
        [Header("Sharing the train with another crane")]
        [Tooltip("Optional. The cranes take turns: each unloads every other train, starting with the one marked Goes First.")]
        [SerializeField] GrabCrane partner;
        [SerializeField] bool goesFirst = true;

        bool myTurn;

        float cableTopLocalY;   //Cable top, in the trolley's space
        int unloadIndex = -1, loadIndex = -1;
        bool busy;

        //Any loaded scene, inactive objects included (unloaded ore loads are switched off)
        static Transform FindAnywhere(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == name) return t;
            }
            return null;
        }

        //Re-link the parts that live in the core scene (train, ore cars, loads)
        void Relink()
        {
            if (train == null) train = FindFirstObjectByType<TrainLoop>();
            if (oreCarNames != null && oreCarNames.Length > 0)
            {
                if (oreCars == null || oreCars.Length != oreCarNames.Length) oreCars = new Transform[oreCarNames.Length];
                for (int i = 0; i < oreCarNames.Length; i++) if (oreCars[i] == null) oreCars[i] = FindAnywhere(oreCarNames[i]);
            }
            if (oreLoadNames != null && oreLoadNames.Length > 0)
            {
                if (oreLoads == null || oreLoads.Length != oreLoadNames.Length) oreLoads = new GameObject[oreLoadNames.Length];
                for (int i = 0; i < oreLoadNames.Length; i++) if (oreLoads[i] == null) { var t = FindAnywhere(oreLoadNames[i]); oreLoads[i] = t != null ? t.gameObject : null; }
            }
        }

        void Awake()
        {
            Relink();
            myTurn = partner == null || goesFirst;
            //The parts come in as siblings from the model; chain them so each carries the next
            if (trolley != null && gantry != null) trolley.SetParent(gantry, true);
            if (grab != null && trolley != null) grab.SetParent(trolley, true);
            if (grabOre != null && grab != null) { grabOre.transform.SetParent(grab, true); grabOre.SetActive(false); }
            for (int i = 0; i < oreCars.Length && i < oreLoads.Length; i++)
                if (oreCars[i] != null && oreLoads[i] != null) oreLoads[i].transform.SetParent(oreCars[i], true);
            if (cable != null && trolley != null) cableTopLocalY = trolley.InverseTransformPoint(cable.position + Vector3.up * cable.lossyScale.y).y;
            //Park the grab at travel height so the taller loco (cab roof gear ~3.75 m above the rail) clears it on its first lap
            if (grab != null) { var p = grab.position; p.y = Mathf.Max(p.y, travelY); grab.position = p; }
        }

        void OnEnable()
        {
            if (train != null) train.Arrived += OnArrived;
        }

        void OnDisable()
        {
            if (train != null) train.Arrived -= OnArrived;
            //Yard unloaded (or crane switched off) mid-unload: never leave the train held
            if (busy && train != null) train.Hold = false;
            busy = false;
        }

        void Start()
        {
            if (train != null)
            {
                unloadIndex = train.StationIndex(unloadStation);
                loadIndex = train.StationIndex(loadStation);
                //The train announces its first stop in its own Start; if that ran first, catch up here
                if (train.Stopped && train.CurrentStationName == unloadStation) OnArrived(unloadIndex);
            }
        }

        void OnArrived(int station)
        {
            if (unloadIndex < 0 && train != null) { unloadIndex = train.StationIndex(unloadStation); loadIndex = train.StationIndex(loadStation); }
            if (station == loadIndex) foreach (var l in oreLoads) if (l != null) l.SetActive(true);
            if (station == unloadIndex && !busy && myTurn && AnyLoaded()) StartCoroutine(Unload());
        }

        bool AnyLoaded()
        {
            foreach (var l in oreLoads) if (l != null && l.activeSelf) return true;
            return false;
        }

        IEnumerator Unload()
        {
            busy = true;
            train.Hold = true;
            for (int i = 0; i < oreCars.Length; i++)
            {
                if (i >= oreLoads.Length || oreLoads[i] == null || !oreLoads[i].activeSelf) continue;
                Vector3 car = oreCars[i].position;
                yield return Hoist(travelY);
                yield return Parallel(GantryTo(car.z), TrolleyTo(car.x));
                yield return Hoist(carGrabY);
                yield return new WaitForSeconds(grabPause);
                oreLoads[i].SetActive(false);
                if (grabOre != null) grabOre.SetActive(true);
                yield return Hoist(travelY);
                yield return TrolleyTo(pileX);
                yield return Hoist(pileDropY);
                yield return new WaitForSeconds(grabPause * 0.5f);
                if (grabOre != null) grabOre.SetActive(false);
            }
            yield return Hoist(travelY);
            if (oreCars.Length > 0) yield return TrolleyTo(oreCars[0].position.x);
            train.Hold = false;
            busy = false;
            if (partner != null) { myTurn = false; partner.myTurn = true; }
        }

        IEnumerator Parallel(IEnumerator a, IEnumerator b)
        {
            bool runA = true, runB = true;
            while (runA || runB)
            {
                if (runA) runA = a.MoveNext();
                if (runB) runB = b.MoveNext();
                yield return null;
            }
        }

        IEnumerator GantryTo(float z)
        {
            z = Mathf.Clamp(z, gantryMinZ, gantryMaxZ);
            while (Mathf.Abs(gantry.position.z - z) > 0.01f)
            {
                var p = gantry.position; p.z = Mathf.MoveTowards(p.z, z, gantrySpeed * Time.deltaTime); gantry.position = p;
                yield return null;
            }
        }

        IEnumerator TrolleyTo(float x)
        {
            while (Mathf.Abs(trolley.position.x - x) > 0.01f)
            {
                var p = trolley.position; p.x = Mathf.MoveTowards(p.x, x, trolleySpeed * Time.deltaTime); trolley.position = p;
                yield return null;
            }
        }

        IEnumerator Hoist(float y)
        {
            while (Mathf.Abs(grab.position.y - y) > 0.01f)
            {
                var p = grab.position; p.y = Mathf.MoveTowards(p.y, y, hoistSpeed * Time.deltaTime); grab.position = p;
                yield return null;
            }
        }

        void LateUpdate()
        {
            if (cable == null || grab == null || trolley == null) return;
            //Stretch the cables from the trolley's hoist drum down to the grab's head block
            float top = trolley.TransformPoint(new Vector3(0f, cableTopLocalY, 0f)).y;
            float bottom = grab.position.y + 0.6f;
            cable.position = new Vector3(grab.position.x, bottom, grab.position.z);
            var s = cable.localScale; s.y = Mathf.Max(0.05f, top - bottom); cable.localScale = s;
        }
    }
}
