using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Crew.Battle
{
    //The battle bar across the top of the HUD (only while a ShipBattle runs): your hull, the enemy's hull, the next
    //volley and boarding, a chip per station (who mans it, its effect, damage), the latest event, and the end panel.
    //Built by Tools > Company > Build Ship Battle.
    public class ShipBattleHUD : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TextMeshProUGUI shipText;
        [SerializeField] Image hullBar;
        [SerializeField] TextMeshProUGUI enemyText;
        [SerializeField] Image enemyHullBar;
        [SerializeField] TextMeshProUGUI timersText;
        [Tooltip("Navigation, Weapons, Engines, Systems.")]
        [SerializeField] Image[] stationChips = new Image[4];
        [SerializeField] TextMeshProUGUI[] stationTexts = new TextMeshProUGUI[4];
        [SerializeField] TextMeshProUGUI logText;

        [Header("End")]
        [SerializeField] GameObject endPanel;
        [SerializeField] TextMeshProUGUI endTitle;
        [SerializeField] TextMeshProUGUI endBody;
        [SerializeField] Button continueButton;

        float timer;
        Rambler.UI.ShipMapPanel map;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            if (endPanel != null) endPanel.SetActive(false);
            if (continueButton != null) continueButton.onClick.AddListener(() => { if (ShipBattle.Instance != null) ShipBattle.Instance.Leave(); });
        }

        void Update()
        {
            var b = ShipBattle.Instance;
            //Out of the way while the ship map (the Systems station) is up, unless the battle is over
            if (map == null) map = FindObjectOfType<Rambler.UI.ShipMapPanel>(true);
            bool show = b != null && (map == null || !map.IsOpen || b.State != BattleState.Fighting);
            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (b == null) return;
            timer += Time.unscaledDeltaTime;
            if (timer < 0.1f) return;
            timer = 0f;

            Set(shipText, b.ShipName.ToUpperInvariant() + "   " + b.Hull + " / " + b.MaxHull);
            if (hullBar != null) hullBar.fillAmount = (float)b.Hull / Mathf.Max(1, b.MaxHull);
            Set(enemyText, b.EnemyName.ToUpperInvariant() + "   " + b.EnemyHull + " / " + b.EnemyMaxHull);
            if (enemyHullBar != null) enemyHullBar.fillAmount = (float)b.EnemyHull / Mathf.Max(1, b.EnemyMaxHull);
            int boarders = b.BoardersAlive;
            Set(timersText, (b.EnemyWeaponsDown ? "Their guns are down" : "Volley in " + Mathf.CeilToInt(b.NextVolley) + "s") + "     " +
                            (b.EnemyEnginesDown ? "No boarders coming" : "Boarders in " + Mathf.CeilToInt(b.NextBoarding) + "s") +
                            (boarders > 0 ? "     <color=#E64033>" + boarders + " aboard!</color>" : "") +
                            (b.Escaping ? "     Jump " + Mathf.RoundToInt(b.EscapeCharge * 100f) + "%" : "") +
                            (b.BreachSummary != null ? "     <color=#4CA6F2>BREACH: " + b.BreachSummary + " (no air)</color>" : ""));

            for (int i = 0; i < 4; i++)
            {
                var s = (Station)i;
                var t = b.Terminal(s);
                string who = t != null ? t.OperatorName : null;
                bool dmg = b.IsDamaged(s);
                if (i < stationChips.Length && stationChips[i] != null)
                {
                    var c = StationTerminal.ColorFor(s);
                    stationChips[i].color = dmg ? new Color(0.5f, 0.12f, 0.1f, 0.95f) : who != null ? new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.95f) : new Color(0.1f, 0.13f, 0.17f, 0.9f);
                }
                if (i < stationTexts.Length)
                    Set(stationTexts[i], "<b>" + (s == Station.Systems ? "SYSTEMS" : s.ToString().ToUpperInvariant()) + "</b>  " + Mathf.RoundToInt(b.Effect(s) * 100f) + "%\n" +
                                         (dmg ? "DAMAGED" : who ?? "empty"));
            }
            Set(logText, b.LastLog);

            if (b.State != BattleState.Fighting && endPanel != null && !endPanel.activeSelf)
            {
                endPanel.SetActive(true);
                switch (b.State)
                {
                    case BattleState.Won:
                        Set(endTitle, "<color=#59CC73>VICTORY</color>");
                        Set(endBody, "The " + b.EnemyName + " is destroyed.\nReward: " + b.Reward.ToString("N0") + " credits.\nHull " + b.Hull + " / " + b.MaxHull + ". Repairs at the Shipyard.");
                        break;
                    case BattleState.Escaped:
                        Set(endTitle, "<color=#4CA6F2>ESCAPED</color>");
                        Set(endBody, "The jump drive fires and the " + b.EnemyName + " is left behind.\nHull " + b.Hull + " / " + b.MaxHull + ".");
                        break;
                    default:
                        Set(endTitle, "<color=#E64033>DEFEAT</color>");
                        Set(endBody, b.Hull <= 0 ? "The ship is crippled and limps home.\nIt needs major repairs at the Shipyard." : "The squad is down.");
                        break;
                }
            }
        }

        static void Set(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
    }
}
