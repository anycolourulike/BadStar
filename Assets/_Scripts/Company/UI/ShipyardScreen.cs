using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Crew.UI
{
    //The Shipyard window, opened at a ShipyardTerminal. Three tabs:
    //  For Sale  ships to buy: list, the ship turning in 3D, its numbers and slots, Buy
    //  My Ships  owned ships: fitted parts (tap a slot to take a part off), spare parts to fit, Set Active,
    //            Repair, Cargo run / Collect, Sell
    //  Parts     ship parts for credits (bought parts go to the spare parts)
    //The game pauses while it is open. Built by Tools > Company > Build Shipyard and Broker.
    public class ShipyardScreen : CompanyWindow
    {
        enum Tab { ForSale, MyShips, Parts }

        public static ShipyardScreen Instance { get; private set; }
        public static ShipyardScreen Find() => Instance != null ? Instance : FindObjectOfType<ShipyardScreen>(true);

        [SerializeField] Button forSaleTab;
        [SerializeField] Button myShipsTab;
        [SerializeField] Button partsTab;

        [Header("Ships page")]
        [SerializeField] GameObject shipsPage;
        [SerializeField] CrewUIItem shipRowTemplate;
        [SerializeField] TextMeshProUGUI emptyListText;
        [SerializeField] RectTransform previewFrame;
        [SerializeField] ShipPreview preview;
        [SerializeField] GameObject sparesRoot;
        [SerializeField] CrewUIItem spareTemplate;
        [SerializeField] TextMeshProUGUI sparesEmptyText;
        [SerializeField] GameObject detailRoot;
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] TextMeshProUGUI descriptionText;
        [SerializeField] TextMeshProUGUI statsText;
        [SerializeField] CrewUIItem slotsItem;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] Button primaryButton;
        [SerializeField] TextMeshProUGUI primaryLabel;
        [SerializeField] Button secondButton;
        [SerializeField] TextMeshProUGUI secondLabel;
        [SerializeField] Button thirdButton;
        [SerializeField] TextMeshProUGUI thirdLabel;
        [SerializeField] Button fourthButton;
        [SerializeField] TextMeshProUGUI fourthLabel;

        [Header("Parts page")]
        [SerializeField] GameObject partsPage;
        [SerializeField] CrewUIItem partTemplate;

        static readonly ShipSlot[] Slots = { ShipSlot.Engine, ShipSlot.Hull, ShipSlot.Cargo, ShipSlot.Bay, ShipSlot.Weapons, ShipSlot.Utility };

        Tab tab = Tab.ForSale;
        string saleId, shipId, confirmSellId;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            HideTemplates(shipRowTemplate, spareTemplate, partTemplate);
            Wire(forSaleTab, () => ShowTab(Tab.ForSale));
            Wire(myShipsTab, () => ShowTab(Tab.MyShips));
            Wire(partsTab, () => ShowTab(Tab.Parts));
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnOpened()
        {
            tab = Company.Instance != null && Company.Instance.Ships.Count > 0 ? Tab.MyShips : Tab.ForSale;
            confirmSellId = null;
        }

        protected override void OnClosed()
        {
            if (preview != null) preview.Show(null);
        }

        void ShowTab(Tab t)
        {
            tab = t;
            confirmSellId = null;
            Say("");
            Refresh();
        }

        protected override void Draw(Company company)
        {
            Tint(forSaleTab, tab == Tab.ForSale);
            Tint(myShipsTab, tab == Tab.MyShips);
            Tint(partsTab, tab == Tab.Parts);
            if (shipsPage != null) shipsPage.SetActive(tab != Tab.Parts);
            if (partsPage != null) partsPage.SetActive(tab == Tab.Parts);
            var active = company.ActiveShip;
            CrewUIItem.SetText(footerText, "Ships " + company.Ships.Count + (active != null ? "   ·   Active: " + active.DisplayName : ""));

            switch (tab)
            {
                case Tab.ForSale: DrawForSale(company); break;
                case Tab.MyShips: DrawMyShips(company); break;
                case Tab.Parts: DrawParts(company); break;
            }
        }

        // ---------- For Sale ----------

        void DrawForSale(Company company)
        {
            SetPreviewTall(true);
            var forSale = Company.ShipsForSale();
            if (saleId == null || forSale.Find(d => d.ItemID == saleId) == null) saleId = forSale.Count > 0 ? forSale[0].ItemID : null;
            CrewUIItem.SetText(emptyListText, "The shipyard has no ships.\nRun Tools > Company > Create Fleet Content.");
            if (emptyListText != null) emptyListText.gameObject.SetActive(forSale.Count == 0);

            var rows = Fill(shipRowTemplate, forSale.Count);
            ShipDefinition sel = null;
            for (int i = 0; i < rows.Count; i++)
            {
                var d = forSale[i];
                var row = rows[i];
                int owned = 0;
                foreach (var s in company.Ships) if (s.definitionId == d.ItemID) owned++;
                row.SetPortrait(d.Icon, d.DisplayName);
                CrewUIItem.SetText(row.title, d.DisplayName);
                CrewUIItem.SetText(row.side, Money(d.Price) + (owned > 0 ? "   ·   " + owned + " owned" : ""));
                bool on = d.ItemID == saleId;
                if (on) sel = d;
                if (row.highlight != null) row.highlight.enabled = on;
                var id = d.ItemID;
                CrewUIItem.SetButton(row.select, null, null, true, true, () => { saleId = id; Say(""); Refresh(); });
            }

            if (detailRoot != null) detailRoot.SetActive(sel != null);
            if (preview != null) preview.Show(sel);
            if (sel == null) return;

            CrewUIItem.SetText(nameText, sel.DisplayName);
            CrewUIItem.SetText(descriptionText, sel.Description);
            var b = sel.BaseStats;
            CrewUIItem.SetText(statsText,
                "Crew seats   " + b.crewSeats + "\n" +
                "Range   " + b.range + "\n" +
                "Hull   " + b.hull + "\n" +
                "Power   " + b.power + "   ·   Firepower   " + b.firepower + "\n" +
                "Cargo run   " + Money(sel.CargoRunPay) + " in " + Duration(System.TimeSpan.FromMinutes(sel.CargoRunMinutes)));
            for (int i = 0; i < slotsItem.slots.Length && i < Slots.Length; i++) DrawSlot(i, Slots[i], null, false, null);
            CrewUIItem.SetText(statusText, "");

            var def = sel;
            bool ok = company.CanAfford(def.Price);
            CrewUIItem.SetButton(primaryButton, primaryLabel, ok ? "Buy   " + Money(def.Price) : "Needs " + Money(def.Price), true, ok, () =>
            {
                if (!company.BuyShip(def)) return;
                var bought = company.Ships[company.Ships.Count - 1];
                shipId = bought.recordId;
                Say("Bought the " + def.DisplayName + "." + (company.ActiveShip == bought ? " It's your active ship." : ""));
            });
            HideButtons(secondButton, thirdButton, fourthButton);
        }

        // ---------- My Ships ----------

        void DrawMyShips(Company company)
        {
            SetPreviewTall(false);
            var ships = company.Ships;
            if (company.GetShip(shipId) == null) shipId = ships.Count > 0 ? ships[0].recordId : null;
            CrewUIItem.SetText(emptyListText, "No ships yet.\nBuy one on the For Sale tab.");
            if (emptyListText != null) emptyListText.gameObject.SetActive(ships.Count == 0);

            var rows = Fill(shipRowTemplate, ships.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var s = ships[i];
                var row = rows[i];
                var d = s.Definition;
                row.SetPortrait(d != null ? d.Icon : null, s.DisplayName);
                CrewUIItem.SetText(row.title, s.DisplayName);
                CrewUIItem.SetText(row.side, ShipState(company, s) + "   ·   Hull " + Mathf.RoundToInt(s.HullFraction * 100f) + "%");
                if (row.highlight != null) row.highlight.enabled = s.recordId == shipId;
                var id = s.recordId;
                CrewUIItem.SetButton(row.select, null, null, true, true, () => { shipId = id; confirmSellId = null; Say(""); Refresh(); });
            }

            var sel = company.GetShip(shipId);
            if (detailRoot != null) detailRoot.SetActive(sel != null);
            if (sparesRoot != null) sparesRoot.SetActive(sel != null);
            if (preview != null) preview.Show(sel != null ? sel.Definition : null);
            if (sel == null) return;

            var def = sel.Definition;
            var st = sel.Stats;
            var b = def != null ? def.BaseStats : new ShipStats();
            CrewUIItem.SetText(nameText, sel.DisplayName + (company.ActiveShip == sel ? "  <size=70%><color=#F29E33>ACTIVE</color></size>" : ""));
            CrewUIItem.SetText(descriptionText, def != null ? def.Description : "");
            var abilities = sel.Abilities;
            CrewUIItem.SetText(statsText,
                "Crew seats   " + st.crewSeats + Bonus(st.crewSeats - b.crewSeats) + "\n" +
                "Range   " + st.range + Bonus(st.range - b.range) + "\n" +
                "Hull   " + sel.Hull + " / " + sel.MaxHull + "\n" +
                "Power   " + st.power + Bonus(st.power - b.power) + "   ·   Firepower   " + st.firepower + Bonus(st.firepower - b.firepower) + "\n" +
                "Cargo run   " + Money(sel.CargoRunPay) + " in " + Duration(sel.CargoRunTime) +
                (abilities.Count > 0 ? "\nAbilities   " + string.Join(", ", abilities) : ""));

            bool docked = !sel.OnCargoRun;
            for (int i = 0; i < slotsItem.slots.Length && i < Slots.Length; i++)
            {
                var slot = Slots[i];
                var fitted = sel.PartIn(slot);
                var ship = sel;
                DrawSlot(i, slot, fitted, docked, () => { if (company.RemovePart(ship, slot)) Say(fitted.DisplayName + " back to the spare parts."); });
            }

            CrewUIItem.SetText(statusText, sel.CargoDone ? "Back from the cargo run with " + Money(sel.cargoPay) + "."
                : sel.OnCargoRun ? "On a cargo run: back in " + Clock(sel.CargoTimeLeft) + "."
                : "Docked." + (company.ActiveShip == sel ? " The squad flies this ship on missions." : ""));

            //Set active
            bool isActive = company.ActiveShip == sel;
            CrewUIItem.SetButton(primaryButton, primaryLabel, isActive ? "Active ship" : "Set active", true, !isActive && docked, () =>
            {
                if (company.SetActiveShip(sel)) Say(sel.DisplayName + " is now your active ship.");
            });
            //Repair
            int cost = company.RepairCost(sel);
            bool canRepair = sel.damage > 0 && docked && company.CanAfford(cost);
            CrewUIItem.SetButton(secondButton, secondLabel, sel.damage > 0 ? "Repair   " + Money(cost) : "Hull OK", true, canRepair, () =>
            {
                if (company.Repair(sel)) Say(sel.DisplayName + " repaired.");
            });
            //Cargo run / collect
            if (sel.CargoDone)
                CrewUIItem.SetButton(thirdButton, thirdLabel, "Collect   " + Money(sel.cargoPay), true, true, () =>
                {
                    int pay = company.CollectCargo(sel);
                    Say("Cargo run paid " + Money(pay) + "." + (sel.damage > 0 ? " Some wear on the hull." : ""));
                });
            else if (sel.OnCargoRun)
                CrewUIItem.SetButton(thirdButton, thirdLabel, "Back in " + Clock(sel.CargoTimeLeft), true, false, null);
            else
            {
                bool canRun = company.CanStartCargoRun(sel, out string why);
                CrewUIItem.SetButton(thirdButton, thirdLabel, canRun ? "Cargo run" : why, true, canRun, () =>
                {
                    if (company.StartCargoRun(sel)) Say(sel.DisplayName + " is off on a cargo run: " + Money(sel.cargoPay) + " in " + Duration(sel.CargoRunTime) + ".");
                });
            }
            //Sell (tap twice)
            bool canSell = company.CanSellShip(sel, out _);
            bool confirming = confirmSellId == sel.recordId;
            CrewUIItem.SetButton(fourthButton, fourthLabel, confirming ? "Confirm  " + Money(company.SalePrice(sel)) : "Sell   " + Money(company.SalePrice(sel)), true, canSell, () =>
            {
                if (confirmSellId != sel.recordId) { confirmSellId = sel.recordId; Say("Tap again to sell the " + sel.DisplayName + ". Fitted parts go to the spare parts."); Refresh(); return; }
                confirmSellId = null;
                string n = sel.DisplayName; int price = company.SalePrice(sel);
                if (company.SellShip(sel)) Say("Sold the " + n + " for " + Money(price) + ".");
            });

            DrawSpares(company, sel);
        }

        //Spare parts, one row per kind with a count; Fit puts it in its slot on the selected ship
        void DrawSpares(Company company, ShipRecord ship)
        {
            var kinds = new List<ShipPartDefinition>();
            foreach (var id in company.SpareParts)
            {
                var p = Ownables.CatalogAsset.GetFromID<ShipPartDefinition>(id);
                if (p != null && !kinds.Contains(p)) kinds.Add(p);
            }
            kinds.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));
            if (sparesEmptyText != null) sparesEmptyText.gameObject.SetActive(kinds.Count == 0);
            var rows = Fill(spareTemplate, kinds.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var p = kinds[i];
                var row = rows[i];
                row.SetPortrait(p.Icon, p.DisplayName);
                CrewUIItem.SetText(row.title, p.DisplayName);
                CrewUIItem.SetText(row.side, "x" + company.SpareCount(p));
                bool ok = company.CanFitPart(ship, p, out string reason);
                var current = ship.PartIn(p.Slot);
                CrewUIItem.SetText(row.body, p.Slot + "   " + p.Describe() + (current != null ? "   ·   swaps " + current.DisplayName : "") + (!ok ? "   ·   " + reason : ""));
                CrewUIItem.SetButton(row.action, row.actionLabel, "Fit", true, ok, () =>
                {
                    if (company.FitPart(ship, p)) Say(p.DisplayName + " fitted to the " + ship.DisplayName + ".");
                });
            }
        }

        // ---------- Parts ----------

        void DrawParts(Company company)
        {
            if (preview != null) preview.Show(null);
            var parts = Company.PartsForSale();
            var rows = Fill(partTemplate, parts.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var p = parts[i];
                var row = rows[i];
                int spare = company.SpareCount(p);
                row.SetPortrait(p.Icon, p.DisplayName);
                CrewUIItem.SetText(row.title, p.DisplayName);
                CrewUIItem.SetText(row.body, p.Slot + "   ·   " + p.Describe() + (spare > 0 ? "   ·   " + spare + " spare" : "") + (string.IsNullOrEmpty(p.Description) ? "" : "   ·   " + p.Description));
                CrewUIItem.SetText(row.side, Money(p.Price));
                bool ok = company.CanAfford(p.Price);
                CrewUIItem.SetButton(row.action, row.actionLabel, "Buy", true, ok, () => { if (company.BuyPart(p)) Say("Bought " + p.DisplayName + ". Fit it on the My Ships tab."); });
            }
            if (parts.Count == 0) Say("No ship parts for sale. Run Tools > Company > Create Fleet Content.");
        }

        // ---------- Helpers ----------

        //For Sale: the preview fills the middle column. My Ships: it shares it with the spare parts.
        void SetPreviewTall(bool tall)
        {
            if (previewFrame != null) previewFrame.anchorMin = new Vector2(previewFrame.anchorMin.x, tall ? 0f : 0.46f);
            if (sparesRoot != null && tall) sparesRoot.SetActive(false);
        }

        void DrawSlot(int index, ShipSlot slot, ShipPartDefinition fitted, bool canEdit, UnityEngine.Events.UnityAction remove)
        {
            var b = slotsItem.slots[index];
            var label = index < slotsItem.slotLabels.Length ? slotsItem.slotLabels[index] : null;
            CrewUIItem.SetButton(b, label, fitted != null ? fitted.DisplayName : slot.ToString(), true, canEdit && fitted != null, fitted != null ? remove : null);
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = fitted != null ? slotFilled : slotEmpty;
            if (label != null) label.color = fitted != null ? Color.white : dimText;
        }

        static void HideButtons(params Button[] buttons)
        {
            foreach (var b in buttons) if (b != null) b.gameObject.SetActive(false);
        }

        static string Bonus(int extra) => extra > 0 ? "  <color=#F29E33>(+" + extra + ")</color>" : extra < 0 ? "  (" + extra + ")" : "";

        static string ShipState(Company company, ShipRecord s)
        {
            if (s.CargoDone) return "<color=#F29E33>Back: collect</color>";
            if (s.OnCargoRun) return "Cargo run " + Clock(s.CargoTimeLeft);
            return company.ActiveShip == s ? "<color=#F29E33>ACTIVE</color>" : "Docked";
        }
    }
}
