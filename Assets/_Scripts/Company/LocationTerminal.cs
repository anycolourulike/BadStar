using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Control;
using Rambler.Ship;

namespace Rambler.Crew
{
    //A place in the world that opens a Company screen (Home Terminal, Shipyard, Broker): walk the active player
    //into the trigger and a button appears above it; tapping it opens the screen. Works like ShipTerminal.
    [RequireComponent(typeof(Collider))]
    public abstract class LocationTerminal : MonoBehaviour
    {
        [SerializeField] protected string label = "";
        [Tooltip("World space canvas shown while the active player is in range (made at runtime if empty).")]
        [SerializeField] protected Canvas prompt;
        [SerializeField] protected Button openButton;
        [SerializeField] protected Vector3 promptOffset = new Vector3(0f, 2.4f, 0f);

        PlayerSelector playerSelector;
        Camera cam;
        //A character can have several colliders; count them so leaving with one doesn't count as leaving
        readonly Dictionary<GameObject, int> inside = new Dictionary<GameObject, int>();

        protected abstract string DefaultLabel { get; }
        protected abstract Color PromptColor { get; }
        //Whether this terminal's screen exists and is open
        protected abstract bool HasScreen { get; }
        protected abstract bool ScreenOpen { get; }
        protected abstract void OpenScreen();

        protected virtual void Start()
        {
            GetComponent<Collider>().isTrigger = true;
            playerSelector = FindObjectOfType<PlayerSelector>();
            cam = Camera.main;
            if (prompt == null) BuildPrompt();
            prompt.worldCamera = cam; //World space canvases need an event camera to receive taps
            prompt.gameObject.SetActive(false);
            if (openButton != null) openButton.onClick.AddListener(Open);
        }

        void Update()
        {
            bool show = ActivePlayerInRange() && !ScreenOpen;
            if (prompt.gameObject.activeSelf != show) prompt.gameObject.SetActive(show);
            if (show && cam != null) prompt.transform.rotation = cam.transform.rotation;
        }

        public void Open()
        {
            if (!HasScreen) { Debug.LogWarning("[" + GetType().Name + "] No " + DefaultLabel + " screen on the HUD. Rebuild it from Tools > Company."); return; }
            if (!ActivePlayerInRange()) return;
            OpenScreen();
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

        //A plain floating button, for terminals placed without the prefab
        void BuildPrompt()
        {
            var go = new GameObject("Prompt", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = promptOffset;
            go.transform.localScale = Vector3.one * 0.01f;
            prompt = go.GetComponent<Canvas>();
            prompt.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(300f, 104f);

            var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(go.transform, false);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            button.GetComponent<Image>().color = PromptColor;
            openButton = button.GetComponent<Button>();

            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(button.transform, false);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = text.GetComponent<TextMeshProUGUI>();
            tmp.text = string.IsNullOrEmpty(label) ? DefaultLabel : label;
            tmp.fontSize = 44; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        }
    }
}
