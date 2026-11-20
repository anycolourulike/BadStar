using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using Rambler.Attributes;
using Rambler.Control;

namespace Rambler.Mission
{
    //The prototype drone. It starts dormant (its enemy AI switched off). Rambler's hack compromises it; the assassin
    //at the relay then takes the link and the player flies it: tap the ground to move, tap the target to strike
    //(it closes in and detonates), or detonate by hand. Afterwards control goes back to the character at the relay.
    [RequireComponent(typeof(NavMeshAgent))]
    public class PilotDrone : MonoBehaviour
    {
        public enum State { Dormant, Compromised, Piloted, Spent }

        [SerializeField] float flySpeed = 6f;
        [SerializeField] float blastRadius = 3.5f;
        [SerializeField] float blastDamage = 1000f;
        [SerializeField] float strikeDistance = 1.4f;
        [Tooltip("Guards within this distance hear the blast.")]
        [SerializeField] float noiseRadius = 30f;
        [SerializeField] GameObject blastFX;
        [SerializeField] Light statusLight;
        [SerializeField] Color dormantColor = new Color(1f, 0.2f, 0.15f);
        [SerializeField] Color hackedColor = new Color(0.2f, 1f, 0.4f);

        public State Current { get; private set; } = State.Dormant;

        NavMeshAgent agent;
        PlayerSelector selector;
        CameraManager cam;
        PlayerController pausedPilot;
        Transform strikeTarget;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            //A prototype on the bench, not an attacking drone: no enemy brain, nobody's target. Removed, not just
            //switched off: Unity still sends trigger events to disabled scripts, and DroneCon explodes on contact.
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                string n = mb.GetType().Name;
                if (n == "DroneCon" || n == "DroneFOV") { mb.enabled = false; Destroy(mb); }
            }
            gameObject.tag = "Untagged";
            if (statusLight == null) statusLight = GetComponentInChildren<Light>();
            SetLight(dormantColor);
        }

        void Start()
        {
            selector = FindObjectOfType<PlayerSelector>();
            cam = FindObjectOfType<CameraManager>();
            if (agent.isOnNavMesh) agent.isStopped = true;
        }

        //Rambler's hack: it's ours now, though it still looks like it's sleeping
        public void Compromise()
        {
            if (Current != State.Dormant) return;
            Current = State.Compromised;
            SetLight(hackedColor);
            Debug.Log("[Drone] compromised");
        }

        //The assassin takes the link: the player flies the drone until it detonates
        public void TakeControl()
        {
            if (Current != State.Compromised) { Debug.Log("[Drone] link refused: not hacked yet"); return; }
            Current = State.Piloted;
            var pilot = selector != null ? selector.activePlayer : null;
            pausedPilot = pilot != null ? pilot.GetComponent<PlayerController>() : null;
            if (pausedPilot != null) pausedPilot.enabled = false;
            var pilotAgent = pilot != null ? pilot.GetComponent<NavMeshAgent>() : null;
            if (pilotAgent != null && pilotAgent.isOnNavMesh) pilotAgent.isStopped = true;
            if (cam != null) cam.UpdateFollow(transform);
            agent.speed = flySpeed;
            agent.isStopped = false;
            Debug.Log("[Drone] piloted");
        }

        void Update()
        {
            if (Current != State.Piloted) return;
            if (agent.isOnNavMesh && agent.isStopped) agent.isStopped = false; //A stray hit stops movers (Health -> CancelNav); keep flying
            HandleTap();
            if (strikeTarget != null)
            {
                agent.SetDestination(strikeTarget.position);
                Vector3 d = strikeTarget.position - transform.position; d.y = 0f;
                if (d.magnitude <= strikeDistance) Detonate();
            }
        }

        void HandleTap()
        {
            bool began = false; Vector2 pos = default; int finger = -1;
            if (Input.touchCount > 0) { var t = Input.GetTouch(0); began = t.phase == TouchPhase.Began; pos = t.position; finger = t.fingerId; }
            else if (!Application.isMobilePlatform && Input.GetMouseButtonDown(0)) { began = true; pos = Input.mousePosition; }
            if (!began || Camera.main == null) return;
            if (EventSystem.current != null && (finger >= 0 ? EventSystem.current.IsPointerOverGameObject(finger) : EventSystem.current.IsPointerOverGameObject())) return;
            if (DetonateButton.Contains(new Vector2(pos.x, Screen.height - pos.y))) return; //The on-screen button
            var ray = Camera.main.ScreenPointToRay(pos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, ~(1 << 2), QueryTriggerInteraction.Collide)) return;
            var target = hit.collider.GetComponentInParent<MissionTarget>();
            if (target == null && hit.rigidbody != null) target = hit.rigidbody.GetComponent<MissionTarget>();
            if (target != null && !target.IsDead) { strikeTarget = target.transform; return; }
            strikeTarget = null;
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit nh, 2f, NavMesh.AllAreas)) agent.SetDestination(nh.position);
        }

        public void Detonate()
        {
            if (Current != State.Piloted) return;
            Current = State.Spent;
            Vector3 at = transform.position;
            foreach (var col in Physics.OverlapSphere(at, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = col.GetComponentInParent<MissionTarget>();
                if (t != null) t.Kill();
                var h = col.GetComponentInParent<Health>();
                if (h != null && h.gameObject != gameObject && !h.IsDead()) h.TakeDamage(blastDamage, true);
            }
            foreach (var guard in FindObjectsOfType<EnemyAI>())
                if (!guard.IsDead && Vector3.Distance(guard.transform.position, at) <= noiseRadius) guard.HearOf(at);
            if (blastFX != null) Instantiate(blastFX, at, Quaternion.identity);
            Debug.Log("[Drone] detonated");
            ReturnControl();
            Destroy(gameObject);
        }

        void ReturnControl()
        {
            if (pausedPilot != null) pausedPilot.enabled = true;
            var back = selector != null ? selector.activePlayer : null;
            if (cam != null && back != null) cam.UpdateFollow(back.transform);
        }

        void SetLight(Color c) { if (statusLight != null) statusLight.color = c; }

        static Rect DetonateButton => new Rect(Screen.width * 0.78f, Screen.height * 0.78f, Screen.width * 0.18f, Screen.height * 0.12f);
        GUIStyle style;
        void OnGUI()
        {
            if (Current != State.Piloted) return;
            if (style == null) style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * 0.04f), fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(Screen.width * 0.02f, Screen.height * 0.02f, Screen.width * 0.6f, Screen.height * 0.08f),
                      "DRONE LINK - tap to fly, tap the target to strike", style);
            if (GUI.Button(DetonateButton, "DETONATE", style)) Detonate();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, blastRadius);
        }
    }
}
