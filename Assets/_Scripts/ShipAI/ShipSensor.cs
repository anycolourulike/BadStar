using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Ship
{
    //A ship camera or motion sensor. Watches a cone for player characters with a clear line of
    //sight and reports them to the ShipAI, which builds up its detection level.
    public class ShipSensor : MonoBehaviour
    {
        [SerializeField] float range = 12f;
        [Range(1f, 360f)] [SerializeField] float viewAngle = 90f;
        [Tooltip("Detection points per second while a player character is in view (ShipAI alerts at 60).")]
        [SerializeField] float detectionPerSecond = 40f;
        [SerializeField] LayerMask targetMask;
        [SerializeField] LayerMask obstructionMask;
        [Tooltip("How high on the target to aim the line-of-sight check.")]
        [SerializeField] float targetHeight = 1.2f;
        [SerializeField] float checkInterval = 0.2f;
        [SerializeField] bool online = true;

        [Header("Feedback")]
        [Tooltip("Optional. Turns green, amber or red with the ship's alert level.")]
        [SerializeField] Light statusLight;

        readonly Collider[] hits = new Collider[16];
        readonly HashSet<GameObject> seenThisScan = new();
        float timer;

        public ShipRoom Room { get; private set; }
        public bool CanSeePlayer { get; private set; }
        public bool Online => online;

        void Reset()
        {
            targetMask = LayerMask.GetMask("Player");
            obstructionMask = LayerMask.GetMask("Obstruction");
        }

        void OnEnable()
        {
            ShipAI.AlertChanged += UpdateLight;
        }

        void OnDisable()
        {
            ShipAI.AlertChanged -= UpdateLight;
        }

        void Start()
        {
            Room = ShipRoom.At(transform.position);
            UpdateLight(ShipAI.Instance != null ? ShipAI.Instance.Alert : AlertState.Dormant);
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < checkInterval) return;
            timer = 0f;

            var ship = ShipAI.Instance;
            CanSeePlayer = false;
            if (!online || ship == null || ship.Controller != ShipFaction.Hostile) return;
            Scan(ship);
        }

        public void SetOnline(bool isOnline)
        {
            online = isOnline;
            CanSeePlayer = false;
            if (statusLight != null) statusLight.enabled = isOnline;
        }

        void Scan(ShipAI ship)
        {
            seenThisScan.Clear();
            Vector3 eye = transform.position;
            int count = Physics.OverlapSphereNonAlloc(eye, range, hits, targetMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var character = ShipCharacters.Root(hits[i]);
                if (character == null || seenThisScan.Contains(character)) continue;
                if (!ShipCharacters.IsPlayerSide(character) || !ShipCharacters.IsAlive(character)) continue;

                Vector3 toTarget = character.transform.position + Vector3.up * targetHeight - eye;
                if (Vector3.Angle(transform.forward, toTarget) > viewAngle * 0.5f) continue;
                if (Physics.Raycast(eye, toTarget.normalized, toTarget.magnitude, obstructionMask, QueryTriggerInteraction.Ignore)) continue;

                seenThisScan.Add(character);
                CanSeePlayer = true;
                ship.ReportSighting(this, character, detectionPerSecond * checkInterval);
            }
        }

        void UpdateLight(AlertState alert)
        {
            if (statusLight == null) return;
            statusLight.color = alert == AlertState.Dormant ? Color.green
                : alert == AlertState.Suspicious ? new Color(1f, 0.6f, 0f)
                : Color.red;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = CanSeePlayer ? Color.red : new Color(1f, 0.8f, 0f);
            Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + left * range);
            Gizmos.DrawLine(transform.position, transform.position + right * range);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
