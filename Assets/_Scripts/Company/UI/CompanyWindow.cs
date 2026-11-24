using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Crew.UI
{
    //Shared plumbing for the Shipyard and Broker windows: open/close with the game paused, redraw when the
    //Company changes (and once a second, for timers), credits in the corner, a message line, and template lists.
    public abstract class CompanyWindow : MonoBehaviour
    {
        [SerializeField] protected GameObject window;
        [SerializeField] protected Button closeButton;
        [SerializeField] protected TextMeshProUGUI creditsText;
        [SerializeField] protected TextMeshProUGUI footerText;
        [SerializeField] protected TextMeshProUGUI messageText;

        [Header("Look")]
        [SerializeField] protected Color tabOn = new Color(0.95f, 0.62f, 0.2f);
        [SerializeField] protected Color tabOff = new Color(0.16f, 0.24f, 0.32f);
        [SerializeField] protected Color slotFilled = new Color(0.95f, 0.62f, 0.2f);
        [SerializeField] protected Color slotEmpty = new Color(0.12f, 0.17f, 0.23f);
        [SerializeField] protected Color dimText = new Color(1f, 1f, 1f, 0.55f);

        readonly Dictionary<CrewUIItem, List<CrewUIItem>> spawned = new Dictionary<CrewUIItem, List<CrewUIItem>>();
        float timeScaleBefore = 1f;
        bool subscribed;
        float nextTick;

        public bool IsOpen => window != null && window.activeSelf;

        protected virtual void Awake()
        {
            if (window != null) window.SetActive(false);
            Wire(closeButton, Close);
        }

        protected virtual void OnDestroy()
        {
            Unsubscribe();
            if (IsOpen) Time.timeScale = timeScaleBefore;
        }

        public void Open()
        {
            if (Company.Instance == null || window == null) return;
            if (!IsOpen)
            {
                timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            window.SetActive(true);
            Subscribe();
            Say("");
            OnOpened();
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            OnClosed();
            window.SetActive(false);
            Unsubscribe();
            Time.timeScale = timeScaleBefore;
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        //Timers (cargo runs, business income) tick in real time while the game is paused
        void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            Refresh();
        }

        public void Refresh()
        {
            var company = Company.Instance;
            if (company == null || !IsOpen) return;
            CrewUIItem.SetText(creditsText, "Credits  " + company.Credits.ToString("N0"));
            Draw(company);
        }

        protected abstract void Draw(Company company);

        void Subscribe()
        {
            if (subscribed || Company.Instance == null) return;
            Company.Instance.Changed += Refresh;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            if (Company.Instance != null) Company.Instance.Changed -= Refresh;
            subscribed = false;
        }

        // ---------- Helpers ----------

        protected void Say(string message)
        {
            if (messageText != null) messageText.text = message;
        }

        protected static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        protected void Tint(Button b, bool on)
        {
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = on ? tabOn : tabOff;
        }

        protected static void HideTemplates(params CrewUIItem[] templates)
        {
            foreach (var t in templates) if (t != null) t.gameObject.SetActive(false);
        }

        //Copies of the template, one per entry; extra copies are hidden, not destroyed
        protected List<CrewUIItem> Fill(CrewUIItem template, int count)
        {
            if (template == null) return new List<CrewUIItem>();
            if (!spawned.TryGetValue(template, out var list)) spawned[template] = list = new List<CrewUIItem>();
            while (list.Count < count)
            {
                var copy = Instantiate(template, template.transform.parent);
                copy.name = template.name.Replace("Template", "") + " " + list.Count;
                list.Add(copy);
            }
            var shown = new List<CrewUIItem>(count);
            for (int i = 0; i < list.Count; i++)
            {
                list[i].gameObject.SetActive(i < count);
                if (i < count) shown.Add(list[i]);
            }
            return shown;
        }

        protected static string Money(int amount) => amount.ToString("N0");

        protected static string Clock(System.TimeSpan t)
        {
            if (t.TotalHours >= 1) return (int)t.TotalHours + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
            return t.Minutes + ":" + t.Seconds.ToString("00");
        }

        protected static string Duration(System.TimeSpan t)
        {
            if (t.TotalHours >= 1) return t.TotalHours.ToString("0.#") + " h";
            return Mathf.Max(1, Mathf.RoundToInt((float)t.TotalMinutes)) + " min";
        }
    }
}
