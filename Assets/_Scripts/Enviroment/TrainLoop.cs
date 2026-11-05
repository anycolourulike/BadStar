using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.AI.Navigation;
using Rambler.Control;
using Rambler.Movement;
using Rambler.Ship;

namespace Rambler.Mine
{
    //Ore train running a closed track path (two balloon loops joined by the main line). It stops at each station on a
    //timetable. The passenger car is walk-on / walk-off: while the train stands, the car's deck NavMesh and door links
    //are live so characters walk in and out; when it departs, everyone standing in the cabin rides along where they
    //stand, and is handed back to the NavMesh at the next stop.
    public class TrainLoop : MonoBehaviour
    {
        [Serializable]
        public class Car
        {
            public Transform body;
            public float length = 4f;
            public bool passenger;
        }

        [Serializable]
        public class Station
        {
            public string name;
            [Tooltip("Path distance where the passenger car's centre stops")] public float stopDistance;
            [Tooltip("Unused since walk-on boarding")] public Transform exitPoint;
            public LiftZone zone;
            public Canvas panel;
            [Tooltip("Unused since walk-on boarding; hidden at start")] public Button boardButton;
            public Text info;
            [Tooltip("Seconds the train stands here; below zero uses the train's Dwell Time")] public float dwell = -1f;
        }

        [Tooltip("Closed path along the rail tops, in travel order (world space)")] [SerializeField] Vector3[] path;
        [Tooltip("Front (loco) first")] [SerializeField] Car[] cars;
        [SerializeField] float gap = 0.6f;
        [SerializeField] Station[] stations;
        [SerializeField] int startStation;
        [SerializeField] float maxSpeed = 6f;
        [Tooltip("Top speed on the steep rack sections")] [SerializeField] float rackSpeed = 2.5f;
        [SerializeField] float rackGrade = 0.04f;
        [SerializeField] float acceleration = 0.8f;
        [SerializeField] float dwellTime = 20f;

        [Header("Walk-on passenger car")]
        [Tooltip("NavMesh of the car's standing deck (child of the passenger car, baked in its local space)")] [SerializeField] NavMeshSurface paxDeck;
        [Tooltip("Door links from the deck to the platform, live only while the train stands")] [SerializeField] NavMeshLink[] paxDoors;
        [Tooltip("Cabin volume in the passenger car's local space: anyone inside rides along")] [SerializeField] Vector3 cabinCentre = new Vector3(0f, 1.1f, 0f);
        [SerializeField] Vector3 cabinSize = new Vector3(2.5f, 2.3f, 2.5f);

        [Serializable]
        public class Gate
        {
            public ShipDoor door;
            [Tooltip("The gate door's name: the door lives in a streamed area scene, so it is found again by name whenever that area loads")]
            public string doorName;
            [NonSerialized] public float nextLookup;
            [NonSerialized] public float[] crossings;   //Path distances where the track passes through the doorway
            [NonSerialized] public float openSince = -1f;
            [NonSerialized] public float emptyFor;
            [NonSerialized] public bool relock;
        }

        [Header("Gates on the line: opened for the train, a sealed gate stops it short")]
        [SerializeField] Gate[] gates;
        [Tooltip("Opens a gate when the loco is this far from it")] [SerializeField] float gateApproach = 40f;
        [Tooltip("Where the train stands when a gate ahead is shut")] [SerializeField] float gateStopShort = 6f;
        [Tooltip("Seconds a gate needs to open before the train runs through")] [SerializeField] float gateOpenTime = 3.5f;
        [Tooltip("Closes a gate this long after the train and the doorway are clear")] [SerializeField] float gateCloseDelay = 5f;

        float trainLength;
        float[] cum;
        float total;
        float head;          //Path distance of the loco's front
        float speed;
        float dwell;
        int next;            //Station the train is heading to (or standing at while dwelling)
        bool stopped;
        Car pax;
        float paxBack;       //Distance from the loco front back to the passenger car's centre
        PlayerSelector playerSelector;

        class Rider
        {
            public Transform root;
            public NavMeshAgent agent;
            public Rigidbody body;
            public bool wasKinematic;
            public Vector3 localPos;        //Where they stand, in the passenger car's space
            public Quaternion localRot;
        }
        readonly List<Rider> riders = new List<Rider>();

        public bool Stopped => stopped;
        //Set by station equipment (e.g. the unloading crane) to keep the train standing until it is done
        public bool Hold { get; set; }
        public event Action<int> Arrived;   //Station index the train has just stopped at
        public event Action<int> Departed;  //Station index the train has just left
        public Car[] Cars => cars;
        public int StationIndex(string stationName) => Array.FindIndex(stations, s => s.name == stationName);
        public string CurrentStationName => stopped ? stations[next].name : "";

        void OnEnable() => PlayerSelector.OnPlayerObjectChanged += OnPlayerChanged;
        void OnDisable() => PlayerSelector.OnPlayerObjectChanged -= OnPlayerChanged;
        void OnPlayerChanged(GameObject _) => RefreshPanels();

        void Start()
        {
            var core = GameObject.Find("PlayerCore");
            if (core != null) playerSelector = core.GetComponent<PlayerSelector>();
            BuildTable();
            float back = 0f;
            foreach (var c in cars)
            {
                if (c.passenger) { pax = c; paxBack = back + c.length * 0.5f; }
                back += c.length + gap;
            }
            trainLength = back;
            FindGateCrossings();
            for (int i = 0; i < stations.Length; i++)
            {
                int index = i;
                var s = stations[i];
                if (s.panel != null) { s.panel.worldCamera = Camera.main; s.panel.gameObject.SetActive(false); }
                if (s.zone != null) s.zone.changed += RefreshPanels;
                if (s.boardButton != null) s.boardButton.gameObject.SetActive(false);   //Walk-on boarding: no BOARD button
            }
            next = Mathf.Clamp(startStation, 0, stations.Length - 1);
            head = Wrap(stations[next].stopDistance + paxBack);
            stopped = true;
            dwell = DwellAt(next);
            PoseCars();
            SetDeck(true);
            RefreshPanels();
            Arrived?.Invoke(next);
        }

        void BuildTable()
        {
            int n = path.Length;
            cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(path[i], path[(i + 1) % n]);
            total = cum[n];
        }

        float Wrap(float d) => ((d % total) + total) % total;

        float DwellAt(int station) => stations[station].dwell >= 0f ? stations[station].dwell : dwellTime;

        //Where the path passes through each gate's doorway plane, within its width
        void FindGateCrossings()
        {
            if (gates == null) return;
            foreach (var g in gates) FindGateCrossings(g);
        }

        void FindGateCrossings(Gate g)
        {
            var found = new List<float>();
            if (g.door != null)
            {
                var t = g.door.transform;
                float halfWidth = g.door.OpeningSize.x * 0.5f + 1f;
                //Only crossings through the doorway count: track passing above or below it (e.g. the spiral
                //under the tunnel portal) cuts the same plane at another height
                float halfHeight = g.door.OpeningSize.y * 0.5f + 1f;
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 a = t.InverseTransformPoint(path[i]), b = t.InverseTransformPoint(path[(i + 1) % path.Length]);
                    if ((a.z < 0f) == (b.z < 0f)) continue;
                    float f = a.z / (a.z - b.z);
                    if (Mathf.Abs(Mathf.Lerp(a.x, b.x, f)) > halfWidth) continue;
                    if (Mathf.Abs(Mathf.Lerp(a.y, b.y, f)) > halfHeight) continue;
                    found.Add(cum[i] + f * (cum[i + 1] - cum[i]));
                }
            }
            g.crossings = found.ToArray();
        }

        //A gate whose area scene is not loaded has no door: the train runs through. Once the area loads, the door
        //is found again by name and its crossings worked out.
        void RelinkGate(Gate g)
        {
            if (string.IsNullOrEmpty(g.doorName) || Time.time < g.nextLookup) return;
            g.nextLookup = Time.time + 1f;
            foreach (var d in ShipDoor.All)
            {
                if (d == null || d.name != g.doorName) continue;
                g.door = d; g.openSince = -1f; g.emptyFor = 0f; g.relock = false;
                FindGateCrossings(g);
                return;
            }
        }

        //Opens gates ahead of the train and shuts them behind it. Returns how far the train may run before a gate
        //that is still shut (sealed, or not yet fully open).
        float UpdateGates()
        {
            float room = float.MaxValue;
            if (gates == null) return room;
            foreach (var g in gates)
            {
                if (g.door == null) RelinkGate(g);
                var door = g.door;
                if (door == null || g.crossings == null || door.IsBroken) continue;
                bool approaching = false, inside = false;
                float ahead = float.MaxValue;
                foreach (float c in g.crossings)
                {
                    float toGate = Wrap(c - head), past = Wrap(head - c);
                    if (past <= trainLength + 1f) inside = true;
                    else if (toGate <= gateApproach) { approaching = true; ahead = Mathf.Min(ahead, toGate); }
                }

                //Never shut a gate on the train: if it is locked while the train is in it, lock it again once clear
                if (inside && door.IsSealed) { door.SetSealed(false); g.relock = true; }
                if ((approaching || inside) && door.State == ShipDoor.DoorState.Closed) door.Open();
                if (door.State == ShipDoor.DoorState.Open) { if (g.openSince < 0f) g.openSince = Time.time; }
                else g.openSince = -1f;
                bool passable = inside || (door.State == ShipDoor.DoorState.Open && Time.time - g.openSince >= gateOpenTime);
                if (approaching && !passable) room = Mathf.Min(room, ahead - gateStopShort);

                //Train clear: shut (or re-lock) the gate once nobody has been in the doorway for a while
                if (approaching || inside || door.DoorwayOccupants().Count > 0) { g.emptyFor = 0f; continue; }
                g.emptyFor += Time.deltaTime;
                if (g.emptyFor < gateCloseDelay) continue;
                if (g.relock) { door.SetSealed(true); g.relock = false; }
                else if (door.State == ShipDoor.DoorState.Open) door.Close();
            }
            return room;
        }

        Vector3 Eval(float d)
        {
            d = Wrap(d);
            int lo = 0, hi = path.Length - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (cum[mid] <= d) lo = mid; else hi = mid - 1; }
            float seg = cum[lo + 1] - cum[lo];
            return Vector3.Lerp(path[lo], path[(lo + 1) % path.Length], seg > 1e-5f ? (d - cum[lo]) / seg : 0f);
        }

        void Update()
        {
            if (total <= 0f) return;
            float gateRoom = UpdateGates();
            if (stopped)
            {
                dwell -= Time.deltaTime;
                var st = stations[next];
                if (st.info != null) st.info.text = dwell <= 0f && Hold ? "UNLOADING" : "DEPARTS IN " + Mathf.CeilToInt(Mathf.Max(0f, dwell));
                if (dwell <= 0f && !Hold)
                {
                    CaptureRiders();
                    SetDeck(false);
                    ParkRiderAgents();
                    stopped = false;
                    int left = next;
                    next = (next + 1) % stations.Length;
                    RefreshPanels();
                    Departed?.Invoke(left);
                }
            }
            else
            {
                float target = Wrap(stations[next].stopDistance + paxBack);
                float remaining = Wrap(target - head);
                Vector3 a = Eval(head), b = Eval(head + 2f);
                float grade = Mathf.Abs(b.y - a.y) / 2f;
                float limit = grade > rackGrade ? rackSpeed : maxSpeed;
                float brake = Mathf.Sqrt(2f * acceleration * remaining);
                speed = Mathf.Min(limit, speed + acceleration * Time.deltaTime, Mathf.Max(brake, 0.25f));
                float step = speed * Time.deltaTime;
                if (gateRoom < remaining)
                {
                    //A shut gate before the next station: brake to a stand short of it and wait for it to open
                    speed = gateRoom <= 0.02f ? 0f : Mathf.Min(speed, Mathf.Max(Mathf.Sqrt(2f * acceleration * gateRoom), 0.25f));
                    head = Wrap(head + Mathf.Min(speed * Time.deltaTime, Mathf.Max(0f, gateRoom)));
                }
                else if (step >= remaining)
                {
                    head = target;
                    speed = 0f;
                    stopped = true;
                    dwell = DwellAt(next);
                    PoseCars();
                    SetDeck(true);
                    ReleaseRiders();
                    RefreshPanels();
                    Arrived?.Invoke(next);
                }
                else head = Wrap(head + step);
            }
            PoseCars();
        }

        void LateUpdate()
        {
            if (pax == null || pax.body == null) return;
            //Riders stand where they were when the train left, carried by the car
            foreach (var r in riders)
            {
                if (r.root == null) continue;
                r.root.position = pax.body.TransformPoint(r.localPos);
                r.root.rotation = pax.body.rotation * r.localRot;
            }
        }

        void PoseCars()
        {
            float d = head;
            foreach (var c in cars)
            {
                float centre = d - c.length * 0.5f;
                Vector3 front = Eval(centre + c.length * 0.32f), rear = Eval(centre - c.length * 0.32f);
                if (c.body != null)
                {
                    c.body.position = (front + rear) * 0.5f;
                    Vector3 fwd = front - rear;
                    if (fwd.sqrMagnitude > 1e-6f) c.body.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                }
                d -= c.length + gap;
            }
        }

        //Deck NavMesh + door links are live only while the train stands (the NavMesh is re-added at the car's pose)
        void SetDeck(bool on)
        {
            if (paxDeck != null)
            {
                paxDeck.RemoveData();
                if (on) paxDeck.AddData();
            }
            if (paxDoors == null) return;
            foreach (var link in paxDoors)
            {
                if (link == null) continue;
                link.enabled = on;
                if (on) link.UpdateLink();
            }
        }

        bool InCabin(Vector3 world)
        {
            Vector3 l = pax.body.InverseTransformPoint(world) - cabinCentre, h = cabinSize * 0.5f;
            return Mathf.Abs(l.x) <= h.x && Mathf.Abs(l.y) <= h.y && Mathf.Abs(l.z) <= h.z;
        }

        //Departing: everyone standing in the cabin rides along where they stand
        void CaptureRiders()
        {
            riders.Clear();
            if (pax == null || pax.body == null) return;
            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                if (!agent.isActiveAndEnabled || !InCabin(agent.transform.position)) continue;
                var t = agent.transform;
                var rider = new Rider { root = t, agent = agent, body = agent.GetComponent<Rigidbody>(),
                                        localPos = pax.body.InverseTransformPoint(t.position), localRot = Quaternion.Inverse(pax.body.rotation) * t.rotation };
                var mover = agent.GetComponent<Mover>();
                if (mover != null && agent.isOnNavMesh) mover.CancelNav();
                if (agent.isOnNavMesh) agent.ResetPath();
                //Keep the agent enabled (taps during the ride must not hit a disabled agent) but stop it driving the transform
                agent.updatePosition = false;
                agent.updateRotation = false;
                if (rider.body != null) { rider.wasKinematic = rider.body.isKinematic; rider.body.isKinematic = true; }
                riders.Add(rider);
            }
        }

        //The deck NavMesh is gone while the train runs: park each rider's (non-driving) agent on the nearest level NavMesh
        //so taps during the ride (Mover.MoveTo) still talk to an agent on a NavMesh. LateUpdate keeps the body in the car.
        void ParkRiderAgents()
        {
            foreach (var r in riders)
            {
                if (r.agent == null || r.root == null) continue;
                if (NavMesh.SamplePosition(r.root.position, out NavMeshHit hit, 8f, NavMesh.AllAreas)) r.agent.Warp(hit.position);
                r.root.position = pax.body.TransformPoint(r.localPos);
                r.root.rotation = pax.body.rotation * r.localRot;
            }
        }

        //Stopped: hand each rider back to the NavMesh where they stand on the deck, so they can walk off
        void ReleaseRiders()
        {
            foreach (var r in riders)
            {
                if (r.root == null) continue;
                r.root.position = pax.body.TransformPoint(r.localPos);
                if (r.body != null) r.body.isKinematic = r.wasKinematic;
                if (r.agent == null) continue;
                if (!r.agent.enabled) r.agent.enabled = true;
                Vector3 p = r.root.position;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) p = hit.position;
                r.agent.Warp(p);
                if (r.agent.isOnNavMesh) r.agent.ResetPath();
                r.agent.updatePosition = true;
                r.agent.updateRotation = true;
            }
            riders.Clear();
        }

        //Departure board at the platform (no BOARD button: just walk on)
        public void RefreshPanels()
        {
            GameObject player = playerSelector != null ? playerSelector.activePlayer : null;
            for (int i = 0; i < stations.Length; i++)
            {
                var s = stations[i];
                if (s.panel == null) continue;
                bool show = stopped && i == next && player != null && s.zone != null && s.zone.Contains(player);
                s.panel.gameObject.SetActive(show);
            }
        }
    }
}
