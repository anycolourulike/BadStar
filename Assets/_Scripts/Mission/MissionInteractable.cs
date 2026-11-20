using UnityEngine;
using UnityEngine.Events;
using Rambler.Control;

namespace Rambler.Mission
{
    //Something only certain squad members can operate: Hayes pulls the breaker, Rambler works the door panel, the
    //assassin takes the drone link. The character the player controls stands in reach and stops; a use bar fills and
    //the action fires. Anyone else is told who it needs.
    public class MissionInteractable : MonoBehaviour
    {
        [Tooltip("Squad members who can use this, by name (a character whose name starts with one of these).")]
        [SerializeField] string[] allowed = { "Hayes" };
        [SerializeField] string label = "Cut power";
        [SerializeField] float reach = 1.8f;
        [SerializeField] float useTime = 1.5f;
        [SerializeField] bool oneShot = true;
        [Tooltip("Can't be used until this is true (set by the mission script, e.g. only during the blackout).")]
        public bool available = true;
        public UnityEvent onUsed;

        PlayerSelector selector;
        float progress;
        bool used;
        GameObject inReach;
        bool rightPerson;

        public bool Used => used;
        public float Progress => useTime > 0f ? progress / useTime : 1f;

        void Start() => selector = FindObjectOfType<PlayerSelector>();

        void Update()
        {
            inReach = null;
            if (used && oneShot) return;
            var player = selector != null ? selector.activePlayer : null;
            if (player == null) return;
            Vector3 d = player.transform.position - transform.position; d.y = 0f;
            if (d.magnitude > reach) { progress = Mathf.Max(0f, progress - Time.deltaTime * 2f); return; }
            inReach = player;
            rightPerson = Allowed(player);
            var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool still = agent == null || agent.velocity.sqrMagnitude < 0.04f;
            if (rightPerson && available && still)
            {
                progress += Time.deltaTime;
                if (progress >= useTime) Use(player);
            }
            else progress = Mathf.Max(0f, progress - Time.deltaTime * 2f);
        }

        bool Allowed(GameObject who)
        {
            foreach (var a in allowed) if (who.name.StartsWith(a)) return true;
            return false;
        }

        public void Use(GameObject by)
        {
            used = true;
            progress = 0f;
            Debug.Log("[Mission] " + (by != null ? by.name : "?") + ": " + label);
            onUsed?.Invoke();
        }

        GUIStyle style;
        void OnGUI()
        {
            if (inReach == null || Camera.main == null) return;
            Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
            if (sp.z < 0f) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(Screen.height * 0.028f), alignment = TextAnchor.MiddleCenter };
            string text;
            if (!rightPerson) text = label + " - needs " + string.Join(" / ", allowed);
            else if (!available) text = label + " - not now";
            else text = label + "  " + new string('|', Mathf.RoundToInt(Progress * 10f)).PadRight(10, '.');
            float w = Screen.width * 0.32f, h = Screen.height * 0.06f;
            GUI.Box(new Rect(sp.x - w / 2f, Screen.height - sp.y - h / 2f, w, h), text, style);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
