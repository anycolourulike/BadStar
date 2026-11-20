using UnityEngine;
using UnityEngine.AI;

namespace Rambler.Mission
{
    //A maglock door. Sealed while the building has power; slides open in a blackout and slams shut again when the
    //backup generators kick in. Something on the far side (an inner panel) can also open it for a while.
    //Put it on the door panel; it moves the panel and switches its NavMeshObstacle so agents path through only when open.
    public class PoweredLock : MonoBehaviour
    {
        [Tooltip("Where the panel slides to when open, relative to its closed position.")]
        [SerializeField] Vector3 openOffset = new Vector3(0f, 2.6f, 0f);
        [SerializeField] float slideTime = 0.5f;
        [Tooltip("Opens in a blackout. Off: only Unlock() opens it.")]
        [SerializeField] bool opensInBlackout = true;
        [SerializeField] Color lockedColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] Color openColor = new Color(0.2f, 0.9f, 0.3f);
        [Tooltip("Lamp showing the lock state (optional).")]
        [SerializeField] Renderer statusLamp;

        Vector3 closedPos;
        float openAmount; //0 closed, 1 open
        float unlockedUntil;
        NavMeshObstacle obstacle;
        MaterialPropertyBlock block;

        public bool IsOpen => Wanted();

        void Awake()
        {
            closedPos = transform.localPosition;
            obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null) { obstacle = gameObject.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; }
            obstacle.carving = true;
            block = new MaterialPropertyBlock();
        }

        //Held open from the other side for a while (an inner control panel)
        public void Unlock(float seconds) => unlockedUntil = Mathf.Max(unlockedUntil, Time.time + seconds);

        bool Wanted()
        {
            if (Time.time < unlockedUntil) return true;
            var grid = PowerGrid.Instance;
            return opensInBlackout && grid != null && !grid.HasPower;
        }

        void Update()
        {
            bool open = Wanted();
            openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, slideTime));
            transform.localPosition = closedPos + openOffset * Mathf.SmoothStep(0f, 1f, openAmount);
            obstacle.enabled = openAmount < 0.9f;
            if (statusLamp != null)
            {
                statusLamp.GetPropertyBlock(block);
                Color c = open ? openColor : lockedColor;
                block.SetColor("_BaseColor", c); block.SetColor("_EmissionColor", c * 2f);
                statusLamp.SetPropertyBlock(block);
            }
        }
    }
}
