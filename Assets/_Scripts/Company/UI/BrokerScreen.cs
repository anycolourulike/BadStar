using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Crew.UI
{
    //The Broker window, opened at a BrokerTerminal. Two tabs:
    //  For Sale  businesses not yet owned: what each earns per mission and per hour, Buy
    //  Owned     the Company's businesses: level, income ready to collect (it builds up in real time to a cap),
    //            Collect, Upgrade, Sell
    //The game pauses while it is open. Built by Tools > Company > Build Shipyard and Broker.
    public class BrokerScreen : CompanyWindow
    {
        enum Tab { ForSale, Owned }

        public static BrokerScreen Instance { get; private set; }
        public static BrokerScreen Find() => Instance != null ? Instance : FindObjectOfType<BrokerScreen>(true);

        [SerializeField] Button forSaleTab;
        [SerializeField] Button ownedTab;
        [SerializeField] CrewUIItem rowTemplate;
        [SerializeField] TextMeshProUGUI emptyListText;
        [SerializeField] GameObject detailRoot;
        [SerializeField] CrewUIItem detailHeader;
        [SerializeField] TextMeshProUGUI descriptionText;
        [SerializeField] TextMeshProUGUI statsText;
        [SerializeField] GameObject levelRoot;
        [SerializeField] Image levelBar;
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] GameObject storeRoot;
        [SerializeField] Image storeBar;
        [SerializeField] TextMeshProUGUI storeText;
        [SerializeField] Button primaryButton;
        [SerializeField] TextMeshProUGUI primaryLabel;
        [SerializeField] Button secondButton;
        [SerializeField] TextMeshProUGUI secondLabel;
        [SerializeField] Button thirdButton;
        [SerializeField] TextMeshProUGUI thirdLabel;

        Tab tab = Tab.ForSale;
        string selectedId, confirmSellId;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            HideTemplates(rowTemplate);
            Wire(forSaleTab, () => ShowTab(Tab.ForSale));
            Wire(ownedTab, () => ShowTab(Tab.Owned));
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnOpened()
        {
            tab = Company.Instance != null && Company.Instance.Businesses.Count > 0 ? Tab.Owned : Tab.ForSale;
            selectedId = null;
            confirmSellId = null;
        }

        void ShowTab(Tab t)
        {
            tab = t;
            selectedId = null;
            confirmSellId = null;
            Say("");
            Refresh();
        }

        protected override void Draw(Company company)
        {
            Tint(forSaleTab, tab == Tab.ForSale);
            Tint(ownedTab, tab == Tab.Owned);

            int perMission = 0; float perHour = 0f;
            foreach (var b in company.Businesses)
                if (b.Definition != null) { perMission += b.Definition.IncomePerMission(b.level); perHour += b.Definition.IncomePerHour(b.level); }
            CrewUIItem.SetText(footerText, "Businesses " + company.Businesses.Count + "   ·   " + Money(perMission) + " a mission   ·   " + Money(Mathf.RoundToInt(perHour)) + " an hour");

            //Both tabs list definitions: for sale = not owned, owned = owned
            var list = new List<BusinessDefinition>();
            foreach (var d in Company.BusinessesForSale())
                if ((company.GetBusiness(d) != null) == (tab == Tab.Owned)) list.Add(d);
            if (selectedId == null || list.Find(d => d.ItemID == selectedId) == null) selectedId = list.Count > 0 ? list[0].ItemID : null;

            CrewUIItem.SetText(emptyListText, tab == Tab.Owned ? "You don't own a business yet.\nBuy one on the For Sale tab." : Company.BusinessesForSale().Count == 0 ? "Nothing on the market.\nRun Tools > Company > Create Fleet Content." : "You own every business on the market.");
            if (emptyListText != null) emptyListText.gameObject.SetActive(list.Count == 0);

            BusinessDefinition sel = null;
            var rows = Fill(rowTemplate, list.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var d = list[i];
                var row = rows[i];
                var owned = company.GetBusiness(d);
                row.SetPortrait(d.Icon, d.DisplayName);
                CrewUIItem.SetText(row.title, d.DisplayName);
                CrewUIItem.SetText(row.side, owned != null ? "Level " + owned.level + "   ·   " + (owned.Full ? "<color=#F29E33>Full: " + Money(owned.Ready) + "</color>" : Money(owned.Ready) + " ready") : Money(d.Price));
                bool on = d.ItemID == selectedId;
                if (on) sel = d;
                if (row.highlight != null) row.highlight.enabled = on;
                var id = d.ItemID;
                CrewUIItem.SetButton(row.select, null, null, true, true, () => { selectedId = id; confirmSellId = null; Say(""); Refresh(); });
            }

            if (detailRoot != null) detailRoot.SetActive(sel != null);
            if (sel == null) return;
            var rec = company.GetBusiness(sel);
            detailHeader.SetPortrait(sel.Icon, sel.DisplayName);
            CrewUIItem.SetText(detailHeader.title, sel.DisplayName);
            CrewUIItem.SetText(detailHeader.body, rec != null ? "Owned   ·   Level " + rec.level + " of " + sel.MaxLevel : "For sale   ·   " + Money(sel.Price));
            CrewUIItem.SetText(descriptionText, sel.Description);

            if (rec == null) DrawForSale(company, sel);
            else DrawOwned(company, sel, rec);
        }

        void DrawForSale(Company company, BusinessDefinition d)
        {
            CrewUIItem.SetText(statsText,
                "Pays   " + Money(d.IncomePerMission(1)) + " every mission\n" +
                "Earns   " + Money(Mathf.RoundToInt(d.IncomePerHour(1))) + " an hour, holds up to " + Money(d.Capacity(1)) + "\n" +
                "Upgrades   " + (d.MaxLevel - 1) + " levels, " + Money(d.UpgradeCost(1)) + " for the first");
            if (levelRoot != null) levelRoot.SetActive(false);
            if (storeRoot != null) storeRoot.SetActive(false);
            bool ok = company.CanBuyBusiness(d, out string reason);
            CrewUIItem.SetButton(primaryButton, primaryLabel, ok ? "Buy   " + Money(d.Price) : reason, true, ok, () =>
            {
                if (!company.BuyBusiness(d)) return;
                Say("Bought " + d.DisplayName + ". It starts earning now.");
                tab = Tab.Owned; selectedId = d.ItemID; Refresh();
            });
            if (secondButton != null) secondButton.gameObject.SetActive(false);
            if (thirdButton != null) thirdButton.gameObject.SetActive(false);
        }

        void DrawOwned(Company company, BusinessDefinition d, BusinessRecord b)
        {
            bool top = b.level >= d.MaxLevel;
            CrewUIItem.SetText(statsText,
                "Pays   " + Money(d.IncomePerMission(b.level)) + " every mission" + (top ? "" : "   <color=#F29E33>→ " + Money(d.IncomePerMission(b.level + 1)) + "</color>") + "\n" +
                "Earns   " + Money(Mathf.RoundToInt(d.IncomePerHour(b.level))) + " an hour" + (top ? "" : "   <color=#F29E33>→ " + Money(Mathf.RoundToInt(d.IncomePerHour(b.level + 1))) + "</color>"));

            if (levelRoot != null) levelRoot.SetActive(true);
            if (levelBar != null) levelBar.fillAmount = (float)b.level / d.MaxLevel;
            CrewUIItem.SetText(levelText, "Level " + b.level + " / " + d.MaxLevel);

            int ready = b.Ready, cap = d.Capacity(b.level);
            if (storeRoot != null) storeRoot.SetActive(true);
            if (storeBar != null) storeBar.fillAmount = cap > 0 ? (float)ready / cap : 0f;
            float hoursToFull = d.IncomePerHour(b.level) > 0f ? (cap - b.stored) / d.IncomePerHour(b.level) : 0f;
            CrewUIItem.SetText(storeText, Money(ready) + " / " + Money(cap) + "   " + (ready >= cap ? "<color=#F29E33>Full: collect it</color>" : "full in " + Clock(System.TimeSpan.FromHours(hoursToFull))));

            CrewUIItem.SetButton(primaryButton, primaryLabel, ready > 0 ? "Collect   " + Money(ready) : "Nothing yet", true, ready > 0, () =>
            {
                int got = company.CollectBusiness(b);
                if (got > 0) Say("Collected " + Money(got) + " from " + d.DisplayName + ".");
            });
            bool canUp = company.CanUpgradeBusiness(b, out string why);
            CrewUIItem.SetButton(secondButton, secondLabel, top ? "Top level" : canUp ? "Upgrade   " + Money(d.UpgradeCost(b.level)) : why, true, canUp, () =>
            {
                if (company.UpgradeBusiness(b)) Say(d.DisplayName + " is now level " + b.level + ".");
            });
            bool confirming = confirmSellId == b.recordId;
            int price = company.SalePrice(b);
            CrewUIItem.SetButton(thirdButton, thirdLabel, confirming ? "Confirm  " + Money(price) : "Sell   " + Money(price), true, true, () =>
            {
                if (confirmSellId != b.recordId) { confirmSellId = b.recordId; Say("Tap again to sell " + d.DisplayName + ". Uncollected income is paid out with the sale."); Refresh(); return; }
                confirmSellId = null;
                int total = price + b.Ready;
                if (company.SellBusiness(b)) { Say("Sold " + d.DisplayName + " for " + Money(total) + "."); selectedId = null; }
            });
        }
    }
}
