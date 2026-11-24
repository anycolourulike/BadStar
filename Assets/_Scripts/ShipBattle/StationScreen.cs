using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Crew.Battle
{
    //The screen of the station the active player is standing at (Navigation, Weapons or Engines; Ship Systems
    //opens the ship map). The battle keeps running while it's open. Built by Tools > Company > Build Ship Battle.
    //  Navigation  evasion, and the escape jump (Plot escape / Cancel)
    //  Weapons     charge, firepower, and what to aim at (Hull, Weapons, Engines) with the enemy's system health
    //  Engines     the power pips for Navigation, Weapons and Systems (- / +), and repairs to damaged stations
    public class StationScreen : MonoBehaviour
    {
        public static StationScreen Instance { get; private set; }
        public static StationScreen Find() => Instance != null ? Instance : FindObjectOfType<StationScreen>(true);

        [SerializeField] GameObject window;
        [SerializeField] Button closeButton;
        [SerializeField] Image titleBar;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI operatorText;
        [SerializeField] TextMeshProUGUI statusText;

        [Header("Navigation")]
        [SerializeField] GameObject navPage;
        [SerializeField] TextMeshProUGUI navText;
        [SerializeField] Image escapeBar;
        [SerializeField] TextMeshProUGUI escapeText;
        [SerializeField] Button escapeButton;
        [SerializeField] TextMeshProUGUI escapeLabel;

        [Header("Weapons")]
        [SerializeField] GameObject weaponsPage;
        [SerializeField] Image chargeBar;
        [SerializeField] TextMeshProUGUI weaponText;
        [Tooltip("Hull, Weapons, Engines.")]
        [SerializeField] Button[] targetButtons = new Button[3];
        [SerializeField] Image enemyWeaponsBar;
        [SerializeField] Image enemyEnginesBar;
        [SerializeField] TextMeshProUGUI enemySystemsText;

        [Header("Engines")]
        [SerializeField] GameObject enginesPage;
        [SerializeField] TextMeshProUGUI powerText;
        [Tooltip("Navigation, Weapons, Systems: a label, four pips, minus and plus each.")]
        [SerializeField] TextMeshProUGUI[] powerLabels = new TextMeshProUGUI[3];
        [SerializeField] Image[] pipImages = new Image[12];
        [SerializeField] Button[] minusButtons = new Button[3];
        [SerializeField] Button[] plusButtons = new Button[3];
        [SerializeField] TextMeshProUGUI repairText;
        [Tooltip("One per station (Navigation, Weapons, Engines, Systems); shown when that station is damaged.")]
        [SerializeField] Button[] repairButtons = new Button[4];
        [SerializeField] Image repairBar;

        [Header("Look")]
        [SerializeField] Color on = new Color(0.95f, 0.62f, 0.2f);
        [SerializeField] Color off = new Color(0.16f, 0.24f, 0.32f);
        [SerializeField] Color pipEmpty = new Color(0.12f, 0.17f, 0.23f);

        static readonly Station[] PowerRows = { Station.Navigation, Station.Weapons, Station.Systems };
        static readonly EnemyTarget[] Targets = { EnemyTarget.Hull, EnemyTarget.Weapons, EnemyTarget.Engines };

        StationTerminal current;
        float timer;

        public bool IsOpen => window != null && window.activeSelf;
        public bool IsOpenFor(StationTerminal t) => IsOpen && current == t;

        void Awake()
        {
            Instance = this;
            if (window != null) window.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (escapeButton != null) escapeButton.onClick.AddListener(() => { if (Battle != null) Battle.ToggleEscape(); Refresh(); });
            for (int i = 0; i < targetButtons.Length && i < Targets.Length; i++)
            {
                var t = Targets[i];
                if (targetButtons[i] != null) targetButtons[i].onClick.AddListener(() => { if (Battle != null) Battle.SetTarget(t); Refresh(); });
            }
            for (int i = 0; i < PowerRows.Length; i++)
            {
                var s = PowerRows[i];
                if (i < minusButtons.Length && minusButtons[i] != null) minusButtons[i].onClick.AddListener(() => { if (Battle != null) Battle.RemovePip(s); Refresh(); });
                if (i < plusButtons.Length && plusButtons[i] != null) plusButtons[i].onClick.AddListener(() => { if (Battle != null) Battle.AddPip(s); Refresh(); });
            }
            for (int i = 0; i < repairButtons.Length; i++)
            {
                var s = (Station)i;
                if (repairButtons[i] != null) repairButtons[i].onClick.AddListener(() => { if (Battle != null) Battle.StartRepair(s); Refresh(); });
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static ShipBattle Battle => ShipBattle.Instance;

        public void Open(StationTerminal terminal)
        {
            if (window == null || terminal == null) return;
            current = terminal;
            window.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (window != null) window.SetActive(false);
            current = null;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Battle == null || Battle.State != BattleState.Fighting) { Close(); return; }
            timer += Time.unscaledDeltaTime;
            if (timer < 0.15f) return;
            timer = 0f;
            Refresh();
        }

        void Refresh()
        {
            var b = Battle;
            if (b == null || current == null) return;
            var s = current.Station;
            if (titleText != null) titleText.text = s.ToString().ToUpperInvariant();
            if (titleBar != null) titleBar.color = StationTerminal.ColorFor(s);

            string who = current.OperatorName;
            int pct = Mathf.RoundToInt(b.Effect(s) * 100f);
            Set(operatorText, who != null
                ? "Manned by <b>" + who + "</b>   ·   " + current.SkillName + " " + current.OperatorSkill + "   ·   Effect " + pct + "%"
                : "<color=#E64033>Unmanned</color>   ·   " + (s == Station.Weapons ? "the guns need a gunner" : "running at " + pct + "%"));
            Set(statusText, b.IsDamaged(s) ? "<color=#E64033>DAMAGED: repair it from Engines</color>" : s == Station.Engines ? "" : "Power " + b.Pips(s) + " / " + b.MaxPips);

            if (navPage != null) navPage.SetActive(s == Station.Navigation);
            if (weaponsPage != null) weaponsPage.SetActive(s == Station.Weapons);
            if (enginesPage != null) enginesPage.SetActive(s == Station.Engines);
            switch (s)
            {
                case Station.Navigation: DrawNavigation(b); break;
                case Station.Weapons: DrawWeapons(b); break;
                case Station.Engines: DrawEngines(b); break;
            }
        }

        void DrawNavigation(ShipBattle b)
        {
            Set(navText, "Evading <b>" + Mathf.RoundToInt(b.Evade * 100f) + "%</b> of incoming fire" + (b.Escaping ? "  (halved while the jump charges)" : "") +
                         "\nMore Piloting skill and more power dodge more.");
            if (escapeBar != null) escapeBar.fillAmount = b.EscapeCharge;
            float nav = b.Effect(Station.Navigation);
            Set(escapeText, b.Escaping ? "Jump charging: " + Mathf.RoundToInt(b.EscapeCharge * 100f) + "%" + (nav > 0f ? "   ·   ~" + Mathf.CeilToInt((1f - b.EscapeCharge) * 60f / nav) + "s" : "   ·   no power")
                                       : "Escape jump: " + Mathf.RoundToInt(b.EscapeCharge * 100f) + "% charged");
            Set(escapeLabel, b.Escaping ? "Cancel escape" : "Plot escape");
        }

        void DrawWeapons(ShipBattle b)
        {
            if (chargeBar != null) chargeBar.fillAmount = b.WeaponCharge;
            float reload = b.ReloadTime;
            Set(weaponText, "Firepower <b>" + b.Firepower + "</b>   ·   " + (float.IsInfinity(reload) ? "<color=#E64033>not firing</color>" : "a shot every " + reload.ToString("0.0") + "s") +
                            "\nThe gunner fires on their own; choose what they aim at.");
            for (int i = 0; i < targetButtons.Length && i < Targets.Length; i++)
                if (targetButtons[i] != null && targetButtons[i].targetGraphic != null) targetButtons[i].targetGraphic.color = b.Target == Targets[i] ? on : off;
            float max = Mathf.Max(1, b.EnemySystemMax);
            if (enemyWeaponsBar != null) enemyWeaponsBar.fillAmount = b.EnemyWeapons / max;
            if (enemyEnginesBar != null) enemyEnginesBar.fillAmount = b.EnemyEngines / max;
            Set(enemySystemsText, "Their weapons " + (b.EnemyWeaponsDown ? "<color=#59CC73>DOWN</color>" : b.EnemyWeapons + "") +
                                  "      Their engines " + (b.EnemyEnginesDown ? "<color=#59CC73>DOWN</color>" : b.EnemyEngines + "") +
                                  "\nWeapons down = no volleys.  Engines down = no boarders.  Half damage goes to the hull.");
        }

        void DrawEngines(ShipBattle b)
        {
            bool canChange = b.CanChangePower;
            Set(powerText, "Power <b>" + b.FreePips + "</b> free of " + b.AvailablePower + (b.IsDamaged(Station.Engines) ? "  <color=#E64033>(halved: engines damaged)</color>" : ""));
            for (int r = 0; r < PowerRows.Length; r++)
            {
                var s = PowerRows[r];
                int p = b.Pips(s);
                if (r < powerLabels.Length) Set(powerLabels[r], s + (b.IsDamaged(s) ? "  <color=#E64033>damaged</color>" : ""));
                for (int k = 0; k < 4; k++)
                {
                    int idx = r * 4 + k;
                    if (idx < pipImages.Length && pipImages[idx] != null)
                    {
                        pipImages[idx].color = k < p ? StationTerminal.ColorFor(s) : pipEmpty;
                        pipImages[idx].gameObject.SetActive(k < b.MaxPips);
                    }
                }
                if (r < minusButtons.Length && minusButtons[r] != null) minusButtons[r].interactable = canChange && p > 0;
                if (r < plusButtons.Length && plusButtons[r] != null) plusButtons[r].interactable = canChange && p < b.MaxPips && b.FreePips > 0;
            }

            bool any = false;
            for (int i = 0; i < repairButtons.Length; i++)
            {
                bool dmg = b.IsDamaged((Station)i);
                any |= dmg;
                if (repairButtons[i] == null) continue;
                repairButtons[i].gameObject.SetActive(dmg);
                if (repairButtons[i].targetGraphic != null) repairButtons[i].targetGraphic.color = b.Repairing == (Station)i ? on : off;
            }
            if (repairBar != null) { repairBar.transform.parent.gameObject.SetActive(b.Repairing.HasValue); repairBar.fillAmount = b.RepairProgress; }
            Set(repairText, !any ? "Nothing damaged." : b.Repairing.HasValue ? "Repairing " + b.Repairing.Value + ": " + Mathf.RoundToInt(b.RepairProgress * 100f) + "%" : "Damaged stations: tap one to repair it.");
        }

        static void Set(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
    }
}
