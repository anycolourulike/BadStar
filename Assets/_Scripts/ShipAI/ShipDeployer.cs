using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Core;
using Rambler.Movement;

namespace Rambler.Ship
{
    //The ship's forces. Sits next to ShipAI and sends units from ShipSpawnPoints toward where the
    //player was last seen: drones while the ship is Suspicious, crew once it is on Alert, and one
    //reinforcement wave each Lockdown. Spawned units join the EnemySpawner's enemy list.
    [RequireComponent(typeof(ShipAI))]
    public class ShipDeployer : MonoBehaviour
    {
        public static event Action<ShipUnit, GameObject> Deployed;

        [Header("Units")]
        [SerializeField] GameObject[] dronePrefabs;
        [SerializeField] GameObject[] crewPrefabs;
        [Tooltip("Sent in the Lockdown wave; falls back to crew if empty.")]
        [SerializeField] GameObject[] reinforcementPrefabs;
        [Tooltip("Ship-deployed units alive at once, across all kinds.")]
        [SerializeField] int maxActive = 8;
        [Tooltip("Don't deploy from a point closer than this to a player character.")]
        [SerializeField] float minDistanceFromPlayer = 6f;

        [Header("Drones (Suspicious and above)")]
        [SerializeField] int dronesPerDeploy = 2;
        [SerializeField] float droneCooldown = 20f;

        [Header("Crew (Alert and above)")]
        [SerializeField] int crewPerDeploy = 2;
        [SerializeField] float crewCooldown = 30f;

        [Header("Reinforcements (Lockdown)")]
        [SerializeField] int reinforcementCount = 4;
        [Tooltip("Seconds between the call going out and the wave arriving.")]
        [SerializeField] float reinforcementDelay = 10f;

        ShipAI ship;
        EnemySpawner enemySpawner;
        Transform container;
        readonly List<GameObject> active = new();
        float droneReadyAt;
        float crewReadyAt;
        bool reinforcementsCalled;
        float decisionTimer;

        public int ActiveCount => active.Count;

        void Awake()
        {
            ship = GetComponent<ShipAI>();
        }

        void Start()
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
            container = new GameObject("Ship Deployed Units").transform;
        }

        void OnEnable() => ShipAI.AlertChanged += OnAlertChanged;
        void OnDisable() => ShipAI.AlertChanged -= OnAlertChanged;

        void Update()
        {
            if (ship.Controller != ShipFaction.Hostile) return;
            decisionTimer += Time.deltaTime;
            if (decisionTimer < 1f) return;
            decisionTimer = 0f;

            if (ship.Alert >= AlertState.Suspicious && Time.time >= droneReadyAt && dronePrefabs.Length > 0)
            {
                if (Deploy(ShipUnit.Drone, dronesPerDeploy) > 0) droneReadyAt = Time.time + droneCooldown;
            }
            if (ship.Alert >= AlertState.Alert && Time.time >= crewReadyAt && crewPrefabs.Length > 0)
            {
                if (Deploy(ShipUnit.Crew, crewPerDeploy) > 0) crewReadyAt = Time.time + crewCooldown;
            }
        }

        void OnAlertChanged(AlertState alert)
        {
            if (alert == AlertState.Lockdown && !reinforcementsCalled && ship.Controller == ShipFaction.Hostile)
            {
                reinforcementsCalled = true;
                StartCoroutine(CallReinforcements());
            }
            //A fresh Lockdown later can call another wave
            if (alert <= AlertState.Suspicious) reinforcementsCalled = false;
        }

        IEnumerator CallReinforcements()
        {
            Debug.Log("[ShipAI] Reinforcements called; arriving in " + reinforcementDelay + "s");
            yield return new WaitForSeconds(reinforcementDelay);
            if (ship.Controller != ShipFaction.Hostile) yield break;
            Deploy(ShipUnit.Reinforcement, reinforcementCount);
        }

        //Sends up to count units of a kind toward the player's last known position. Returns how many went.
        public int Deploy(ShipUnit unit, int count)
        {
            Prune();
            var prefabs = PrefabsFor(unit);
            if (prefabs == null || prefabs.Length == 0) return 0;

            int sent = 0;
            for (int i = 0; i < count && active.Count < maxActive; i++)
            {
                var point = PickSpawnPoint(unit);
                if (point == null) break;
                var prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Length)];
                if (prefab == null) continue;

                var spawned = point.Spawn(prefab, container);
                spawned.name = prefab.name + " (" + unit + ")";
                spawned.tag = "Enemy";
                active.Add(spawned);
                if (enemySpawner != null) enemySpawner.RegisterEnemy(spawned);
                StartCoroutine(SendToLastKnown(spawned));
                Deployed?.Invoke(unit, spawned);
                sent++;
            }
            if (sent > 0) Debug.Log("[ShipAI] Deployed " + sent + " " + unit + (sent > 1 ? "s" : ""));
            return sent;
        }

        GameObject[] PrefabsFor(ShipUnit unit)
        {
            switch (unit)
            {
                case ShipUnit.Drone: return dronePrefabs;
                case ShipUnit.Crew: return crewPrefabs;
                default: return reinforcementPrefabs != null && reinforcementPrefabs.Length > 0 ? reinforcementPrefabs : crewPrefabs;
            }
        }

        //The safe point nearest the player's last known position, but not right on top of them
        ShipSpawnPoint PickSpawnPoint(ShipUnit unit)
        {
            ShipSpawnPoint best = null;
            float bestDistance = Mathf.Infinity;
            foreach (var point in ShipSpawnPoint.All)
            {
                if (!point.Serves(unit) || !point.IsSafeFor(unit)) continue;
                if (NearPlayerSide(point.transform.position)) continue;
                float distance = (point.transform.position - ship.LastKnownPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }
            return best;
        }

        bool NearPlayerSide(Vector3 position)
        {
            float limit = minDistanceFromPlayer * minDistanceFromPlayer;
            foreach (var tag in new[] { "Player", "Companion" })
            {
                foreach (var character in GameObject.FindGameObjectsWithTag(tag))
                {
                    if ((character.transform.position - position).sqrMagnitude < limit) return true;
                }
            }
            return false;
        }

        //Head for the last sighting; their own field of view takes over once they see someone
        IEnumerator SendToLastKnown(GameObject unit)
        {
            yield return null; //Let the unit's Awake/Start settle its state machine first
            if (unit == null || ship.LastKnownRoom == null && ship.LastKnownPosition == Vector3.zero) yield break;
            var mover = unit.GetComponent<Mover>();
            if (mover != null) mover.StartMoveAction(ship.LastKnownPosition, 1f);
        }

        //Forget units that died, were destroyed, or were hacked over to the player
        void Prune()
        {
            active.RemoveAll(unit => unit == null || !unit.CompareTag("Enemy") || !ShipCharacters.IsAlive(unit));
        }
    }
}
