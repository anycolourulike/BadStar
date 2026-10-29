using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Control;

namespace Rambler.Ship
{
    //A ship terminal the player can hack to take one system (doors, life support, sensors).
    //Walk the active player into its trigger, tap the button, and stay close while the hack runs.
    //Starting a hack while the ship is hostile sets off an alarm. Hack every terminal to take the ship.
    [RequireComponent(typeof(Collider))]
    public class ShipTerminal : MonoBehaviour
    {
        static readonly List<ShipTerminal> all = new();
        public static IReadOnlyList<ShipTerminal> All => all;
        public static event Action<ShipTerminal> Hacked;

        //True when there is at least one terminal and none is left to hack
        public static bool AllHacked
        {
            get
            {
                if (all.Count == 0) return false;
                foreach (var terminal in all)
                {
                    if (!terminal.IsHacked) return false;
                }
                return true;
            }
        }

        [SerializeField] string terminalName = "Terminal";
        [SerializeField] ShipControl grants = ShipControl.Doors;
        [SerializeField] float hackTime = 4f;
        [Tooltip("Detection the ship gains when a hack starts while it is hostile.")]
        [SerializeField] float alarmOnHack = 35f;

        [Header("Prompt")]
        [Tooltip("World space canvas shown while the active player is in range.")]
        [SerializeField] Canvas prompt;
        [SerializeField] Button hackButton;
        [Tooltip("Radial fill showing hack progress.")]
        [SerializeField] Image progressFill;
        [SerializeField] TextMeshProUGUI label;

        [Header("Look")]
        [SerializeField] Renderer screen;
        [SerializeField] Color lockedColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] Color hackingColor = new Color(1f, 0.75f, 0.2f);
        [SerializeField] Color hackedColor = new Color(0.3f, 1f, 0.45f);

        PlayerSelector playerSelector;
        //A character can have several colliders; count them so leaving with one doesn't count as leaving
        readonly Dictionary<GameObject, int> inside = new();
        GameObject hacker;
        float progress;
        MaterialPropertyBlock block;

        public bool IsHacked { get; private set; }
        public bool IsHacking => hacker != null;
        public ShipControl Grants => grants;
        public string TerminalName => terminalName;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Start()
        {
            GetComponent<Collider>().isTrigger = true;
            var playerCore = GameObject.Find("/PlayerCore");
            if (playerCore != null) playerSelector = playerCore.GetComponent<PlayerSelector>();
            if (prompt != null)
            {
                prompt.worldCamera = Camera.main; //World space canvases need an event camera to receive taps
                prompt.gameObject.SetActive(false);
            }
            if (hackButton != null) hackButton.onClick.AddListener(StartHack);
            if (label != null) label.text = terminalName;
            SetFill(0f);
            UpdateLook();
        }

        void Update()
        {
            if (IsHacked) return;
            if (prompt != null) prompt.gameObject.SetActive(ActivePlayerInRange() || IsHacking);
            if (!IsHacking) return;

            //Break off if the hacker walks away or dies
            if (!inside.ContainsKey(hacker) || !ShipCharacters.IsAlive(hacker))
            {
                CancelHack();
                return;
            }

            progress += Time.deltaTime;
            SetFill(progress / hackTime);
            if (progress >= hackTime) CompleteHack();
        }

        //HUD button on the terminal
        public void StartHack()
        {
            if (IsHacked || IsHacking || !ActivePlayerInRange()) return;
            hacker = playerSelector.activePlayer;
            progress = 0f;
            var ship = ShipAI.Instance;
            if (ship != null && ship.Controller == ShipFaction.Hostile && !ship.PlayerHas(ShipControl.Sensors))
            {
                ship.ReportSighting(null, hacker, alarmOnHack);
            }
            Debug.Log("[Terminal] " + hacker.name + " hacking " + terminalName);
            UpdateLook();
        }

        void CancelHack()
        {
            Debug.Log("[Terminal] Hack of " + terminalName + " interrupted");
            hacker = null;
            progress = 0f;
            SetFill(0f);
            UpdateLook();
        }

        void CompleteHack()
        {
            IsHacked = true;
            hacker = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
            Debug.Log("[Terminal] " + terminalName + " hacked: player has " + grants);
            UpdateLook();
            Hacked?.Invoke(this);
            if (ShipAI.Instance != null) ShipAI.Instance.GrantControl(grants);
        }

        bool ActivePlayerInRange()
        {
            var player = playerSelector != null ? playerSelector.activePlayer : null;
            return player != null && inside.ContainsKey(player);
        }

        void OnTriggerEnter(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null || !ShipCharacters.IsPlayerSide(character)) return;
            inside.TryGetValue(character, out int count);
            inside[character] = count + 1;
        }

        void OnTriggerExit(Collider other)
        {
            var character = ShipCharacters.Root(other);
            if (character == null || !inside.TryGetValue(character, out int count)) return;
            if (count <= 1) inside.Remove(character);
            else inside[character] = count - 1;
        }

        void SetFill(float amount)
        {
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(amount);
        }

        void UpdateLook()
        {
            if (screen == null) return;
            block ??= new MaterialPropertyBlock();
            var color = IsHacked ? hackedColor : IsHacking ? hackingColor : lockedColor;
            screen.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * 2f);
            screen.SetPropertyBlock(block);
        }
    }
}
