using UnityEngine;
using TMPro;
using Rambler.Control;
using Rambler.Ship;

namespace Rambler.UI
{
    //Shows the oxygen and temperature of the room the active player is in, but only when they
    //are human (Rambler doesn't breathe). Turns red/blue when the air becomes dangerous.
    public class AtmosphereHUD : MonoBehaviour
    {
        [SerializeField] PlayerSelector playerSelector;
        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI oxygenText;
        [SerializeField] TextMeshProUGUI temperatureText;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color lowOxygenColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField] Color coldColor = new Color(0.45f, 0.8f, 1f);
        [SerializeField] float refreshInterval = 0.25f;

        float timer;

        void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;

            var player = playerSelector != null ? playerSelector.activePlayer : null;
            var life = player != null ? player.GetComponent<CharacterIdentity>() : null;
            var room = life != null && life.BreathesAir ? life.CurrentRoom : null;

            if (panel != null) panel.SetActive(room != null);
            if (room == null) return;

            oxygenText.text = "O2 " + room.Oxygen.ToString("0") + "%";
            oxygenText.color = life.IsSuffocating ? lowOxygenColor : normalColor;
            temperatureText.text = room.Temperature.ToString("0") + "°C";
            temperatureText.color = life.IsFreezing ? coldColor : normalColor;
        }
    }
}
