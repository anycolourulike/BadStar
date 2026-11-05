using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Rambler.Control;
using Rambler.Movement;
using Rambler.Ship;

namespace Rambler.Mine
{
    //Shaft hoist with a cage that runs between landings. Each landing has a trigger zone and a world space
    //panel; tapping a stop calls the cage if it is elsewhere, loads the player side characters standing in
    //the zone, carries them down/up the shaft and sets them down on the destination landing's NavMesh.
    public class MineLift : MonoBehaviour
    {
        [Serializable]
        public class Stop
        {
            public string name;
            [Tooltip("World height of the cage floor at this landing")] public float cageFloorY;
            [Tooltip("Where riders are set down at this landing (on the NavMesh)")] public Transform exitPoint;
            [Tooltip("Trigger the player stands in to use the panel")] public LiftZone zone;
            public Canvas panel;
            [Tooltip("One button per stop index; the button for this landing itself is hidden")] public Button[] buttons;
        }

        [SerializeField] Transform cage;
        [Tooltip("Cage floor height of the cage model as built")] [SerializeField] float cageFloorAsBuilt;
        [Tooltip("Optional: hoist cables, stretched so they stay pinned to the sheaves")] [SerializeField] Transform cables;
        [SerializeField] float cableTopY;
        [SerializeField] Stop[] stops;
        [SerializeField] int startStop = 1;
        [SerializeField] float maxSpeed = 4f;
        [SerializeField] float acceleration = 2f;
        [SerializeField] float doorPause = 0.6f;   //Pause at each landing while riders step in or out
        [Tooltip("Optional: shown while the cage is moving, e.g. a warning beacon light")] [SerializeField] GameObject movingIndicator;

        public int CurrentStop { get; private set; }
        public bool Busy { get; private set; }

        PlayerSelector playerSelector;
        Vector3 cageBase;              //Cage local position when its floor is at cageFloorAsBuilt
        Vector3 cableBasePos, cableBaseScale;
        float cableBottomAsBuilt;

        void OnEnable() => PlayerSelector.OnPlayerObjectChanged += OnPlayerChanged;
        void OnDisable() => PlayerSelector.OnPlayerObjectChanged -= OnPlayerChanged;
        void OnPlayerChanged(GameObject _) => RefreshPanels();

        void Start()
        {
            var core = GameObject.Find("PlayerCore");
            if (core != null) playerSelector = core.GetComponent<PlayerSelector>();
            cageBase = cage.localPosition;
            if (cables != null)
            {
                cableBasePos = cables.localPosition;
                cableBaseScale = cables.localScale;
                var r = cables.GetComponent<Renderer>();
                cableBottomAsBuilt = r != null ? r.bounds.min.y : cageFloorAsBuilt;
            }
            for (int i = 0; i < stops.Length; i++)
            {
                int index = i;
                var s = stops[i];
                if (s.panel != null)
                {
                    s.panel.worldCamera = Camera.main; //World space canvases need an event camera to receive taps
                    s.panel.gameObject.SetActive(false);
                }
                if (s.zone != null) { s.zone.stopIndex = index; s.zone.changed += RefreshPanels; }
                if (s.buttons == null) continue;
                for (int b = 0; b < s.buttons.Length; b++)
                {
                    int target = b;
                    if (s.buttons[b] != null) s.buttons[b].onClick.AddListener(() => Ride(index, target));
                }
            }
            CurrentStop = Mathf.Clamp(startStop, 0, stops.Length - 1);
            SetCageFloor(stops[CurrentStop].cageFloorY);
            if (movingIndicator != null) movingIndicator.SetActive(false);
        }

        //Called by the landing panels: carry whoever is waiting at 'from' to 'to'
        public void Ride(int from, int to)
        {
            if (Busy || from == to || from < 0 || to < 0 || from >= stops.Length || to >= stops.Length) return;
            StartCoroutine(RideRoutine(from, to));
        }

        //Bring the empty cage to a landing
        public void Call(int stop)
        {
            if (Busy || stop == CurrentStop) return;
            StartCoroutine(TravelRoutine(stop, null));
        }

        IEnumerator RideRoutine(int from, int to)
        {
            Busy = true;
            RefreshPanels();
            if (CurrentStop != from) yield return TravelRoutine(from, null, false);

            var riders = new List<Rider>();
            foreach (var character in stops[from].zone != null ? stops[from].zone.Occupants() : new List<GameObject>())
            {
                if (ShipCharacters.IsPlayerSide(character)) riders.Add(Board(character, riders.Count));
            }
            yield return new WaitForSeconds(doorPause);
            yield return TravelRoutine(to, riders, false);
            yield return new WaitForSeconds(doorPause * 0.5f);
            for (int i = 0; i < riders.Count; i++) SetDown(riders[i], stops[to], i);
            Busy = false;
            RefreshPanels();
        }

        IEnumerator TravelRoutine(int target, List<Rider> riders, bool manageBusy = true)
        {
            if (manageBusy) { Busy = true; RefreshPanels(); }
            if (movingIndicator != null) movingIndicator.SetActive(true);
            float y = CageFloor(), goal = stops[target].cageFloorY, speed = 0f;
            while (!Mathf.Approximately(y, goal))
            {
                float remaining = Mathf.Abs(goal - y);
                float brake = Mathf.Sqrt(2f * acceleration * remaining);  //Slow down in time to stop at the landing
                speed = Mathf.Min(maxSpeed, speed + acceleration * Time.deltaTime, Mathf.Max(brake, 0.3f));
                float next = Mathf.MoveTowards(y, goal, speed * Time.deltaTime);
                float delta = next - y;
                y = next;
                SetCageFloor(y);
                if (riders != null) foreach (var r in riders) if (r.root != null) r.root.position += Vector3.up * delta;
                yield return null;
            }
            CurrentStop = target;
            if (movingIndicator != null) movingIndicator.SetActive(false);
            if (manageBusy) { Busy = false; RefreshPanels(); }
        }

        class Rider
        {
            public Transform root;
            public NavMeshAgent agent;
            public Rigidbody body;
            public bool wasKinematic;
        }

        Rider Board(GameObject character, int slot)
        {
            var rider = new Rider { root = character.transform, agent = character.GetComponent<NavMeshAgent>(), body = character.GetComponent<Rigidbody>() };
            var mover = character.GetComponent<Mover>();
            if (mover != null && rider.agent != null && rider.agent.enabled && rider.agent.isOnNavMesh) mover.CancelNav();
            //Keep the agent enabled (taps during the ride must not hit a disabled agent) but stop it driving the transform
            if (rider.agent != null && rider.agent.enabled)
            {
                if (rider.agent.isOnNavMesh) rider.agent.ResetPath();
                rider.agent.updatePosition = false;
                rider.agent.updateRotation = false;
            }
            if (rider.body != null) { rider.wasKinematic = rider.body.isKinematic; rider.body.isKinematic = true; }
            //Stand riders side by side in the cage
            Vector3 centre = CageCentre();
            rider.root.position = new Vector3(centre.x + (slot - 0.5f) * 0.8f, CageFloor(), centre.z);
            return rider;
        }

        void SetDown(Rider rider, Stop stop, int slot)
        {
            if (rider.root == null) return;
            Vector3 spot = stop.exitPoint != null ? stop.exitPoint.position : rider.root.position;
            spot += stop.exitPoint != null ? stop.exitPoint.right * (slot - 0.5f) * 1.0f : Vector3.zero;
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 2f, NavMesh.AllAreas)) spot = hit.position;
            rider.root.position = spot;
            if (rider.body != null) rider.body.isKinematic = rider.wasKinematic;
            if (rider.agent != null)
            {
                if (!rider.agent.enabled) rider.agent.enabled = true;
                rider.agent.Warp(spot);
                if (rider.agent.isOnNavMesh) rider.agent.ResetPath();
                rider.agent.updatePosition = true;
                rider.agent.updateRotation = true;
            }
        }

        float CageFloor() => cageFloorAsBuilt + (cage.localPosition.y - cageBase.y);

        Vector3 CageCentre()
        {
            var r = cage.GetComponent<Renderer>();
            return r != null ? r.bounds.center : cage.position;
        }

        void SetCageFloor(float y)
        {
            cage.localPosition = cageBase + Vector3.up * (y - cageFloorAsBuilt);
            if (cables == null) return;
            //Cables hang from cableTopY down to the cage roof; scale them about the top so they follow the cage
            float builtLength = cableTopY - cableBottomAsBuilt;
            if (builtLength <= 0.01f) return;
            float cageTop = cableBottomAsBuilt + (y - cageFloorAsBuilt);
            float s = Mathf.Max(0.01f, (cableTopY - cageTop) / builtLength);
            cables.localScale = new Vector3(cableBaseScale.x, cableBaseScale.y * s, cableBaseScale.z);
            //Keep the top fixed: world y = pos + scale * local, so shift by top * (1 - s) in the parent's space
            float topLocal = cableTopY - cables.parent.position.y - cableBasePos.y;
            cables.localPosition = new Vector3(cableBasePos.x, cableBasePos.y + topLocal * (1f - s), cableBasePos.z);
        }

        public void RefreshPanels()
        {
            GameObject player = playerSelector != null ? playerSelector.activePlayer : null;
            for (int i = 0; i < stops.Length; i++)
            {
                var s = stops[i];
                if (s.panel == null) continue;
                bool show = !Busy && player != null && s.zone != null && s.zone.Contains(player);
                s.panel.gameObject.SetActive(show);
                if (!show || s.buttons == null) continue;
                for (int b = 0; b < s.buttons.Length; b++) if (s.buttons[b] != null) s.buttons[b].gameObject.SetActive(b != i);
            }
        }
    }
}
