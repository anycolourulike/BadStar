using UnityEngine;
using Rambler.Control;

namespace Rambler.Ship
{
    //Hides a building's roofs (and anything stacked on them) while the active player is inside
    //one of its rooms, so the top-down camera can see in. Shows them again on the way out.
    public class HabitatCutaway : MonoBehaviour
    {
        [SerializeField] ShipRoom[] rooms;
        [Tooltip("Hidden while the player is inside.")]
        [SerializeField] Renderer[] roofs;
        [SerializeField] float checkInterval = 0.2f;

        PlayerSelector playerSelector;
        bool hidden;
        float timer;

        void Start()
        {
            var playerCore = GameObject.Find("/PlayerCore");
            if (playerCore != null) playerSelector = playerCore.GetComponent<PlayerSelector>();
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < checkInterval) return;
            timer = 0f;

            var player = playerSelector != null ? playerSelector.activePlayer : null;
            bool inside = player != null && IsInside(player.transform.position + Vector3.up);
            if (inside == hidden) return;
            hidden = inside;
            foreach (var roof in roofs)
            {
                if (roof != null) roof.enabled = !hidden;
            }
        }

        bool IsInside(Vector3 position)
        {
            foreach (var room in rooms)
            {
                if (room != null && room.Contains(position)) return true;
            }
            return false;
        }
    }
}
