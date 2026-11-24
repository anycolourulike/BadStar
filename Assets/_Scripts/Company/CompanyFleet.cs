using System;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //Ships and businesses (phase 5). Ships are bought, fitted, repaired and sent on cargo runs at a Shipyard;
    //businesses are bought, upgraded and collected from at a Broker. Saved with the rest of the Company.
    public partial class Company
    {
        [Header("Ships and businesses")]
        [Tooltip("Share of the price paid back when a ship or business is sold.")]
        [Range(0f, 1f)] [SerializeField] float sellBack = 0.5f;
        [Tooltip("A ship can't fly cargo with less hull than this.")]
        [Range(0f, 1f)] [SerializeField] float minCargoHull = 0.25f;

        readonly List<ShipRecord> ships = new List<ShipRecord>();
        readonly List<BusinessRecord> businesses = new List<BusinessRecord>();
        //Ship parts owned but not fitted, one entry each
        readonly List<string> spareParts = new List<string>();
        string activeShipId;

        public IReadOnlyList<ShipRecord> Ships => ships;
        public IReadOnlyList<BusinessRecord> Businesses => businesses;
        public IReadOnlyList<string> SpareParts => spareParts;
        public float SellBack => sellBack;

        //The ship the squad flies on missions (seats, range and support abilities come from it)
        public ShipRecord ActiveShip
        {
            get
            {
                foreach (var s in ships) if (s.recordId == activeShipId) return s;
                return null;
            }
        }

        // ---------- Ships ----------

        public static List<ShipDefinition> ShipsForSale()
        {
            var list = new List<ShipDefinition>(CatalogAsset.All<ShipDefinition>());
            list.Sort((a, b) => a.Price.CompareTo(b.Price));
            return list;
        }

        public static List<ShipPartDefinition> PartsForSale()
        {
            var list = new List<ShipPartDefinition>(CatalogAsset.All<ShipPartDefinition>());
            list.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Price.CompareTo(b.Price));
            return list;
        }

        public bool BuyShip(ShipDefinition d)
        {
            if (d == null || !Spend(d.Price)) return false;
            var s = new ShipRecord(d);
            ships.Add(s);
            if (ActiveShip == null) activeShipId = s.recordId;
            Changed?.Invoke();
            return true;
        }

        public int SalePrice(ShipRecord s) => s != null && s.Definition != null ? Mathf.RoundToInt(s.Definition.Price * sellBack) : 0;

        public bool CanSellShip(ShipRecord s, out string reason)
        {
            reason = null;
            if (s == null || !ships.Contains(s)) reason = "Not yours";
            else if (s.OnCargoRun) reason = "On a cargo run";
            return reason == null;
        }

        //Fitted parts come off into the spare parts first
        public bool SellShip(ShipRecord s)
        {
            if (!CanSellShip(s, out _)) return false;
            spareParts.AddRange(s.partIds);
            s.partIds.Clear();
            credits += SalePrice(s);
            ships.Remove(s);
            if (activeShipId == s.recordId) activeShipId = ships.Count > 0 ? ships[0].recordId : null;
            Changed?.Invoke();
            return true;
        }

        public bool SetActiveShip(ShipRecord s)
        {
            if (s == null || !ships.Contains(s) || s.OnCargoRun) return false;
            activeShipId = s.recordId;
            Changed?.Invoke();
            return true;
        }

        public ShipRecord GetShip(string recordId)
        {
            foreach (var s in ships) if (s.recordId == recordId) return s;
            return null;
        }

        // ---------- Ship parts ----------

        public bool BuyPart(ShipPartDefinition p)
        {
            if (p == null || !Spend(p.Price)) return false;
            spareParts.Add(p.ItemID);
            Changed?.Invoke();
            return true;
        }

        public int SpareCount(ShipPartDefinition p)
        {
            int n = 0;
            if (p != null) foreach (var id in spareParts) if (id == p.ItemID) n++;
            return n;
        }

        public bool CanFitPart(ShipRecord s, ShipPartDefinition p, out string reason)
        {
            reason = null;
            if (s == null || !ships.Contains(s)) reason = "No ship";
            else if (p == null || !spareParts.Contains(p.ItemID)) reason = "Not in spare parts";
            else if (s.OnCargoRun) reason = s.DisplayName + " is on a cargo run";
            return reason == null;
        }

        //Fits it from the spare parts; whatever was in that slot goes back to the spare parts
        public bool FitPart(ShipRecord s, ShipPartDefinition p)
        {
            if (!CanFitPart(s, p, out _)) return false;
            spareParts.Remove(p.ItemID);
            var old = s.PartIn(p.Slot);
            if (old != null) { s.partIds.Remove(old.ItemID); spareParts.Add(old.ItemID); }
            s.partIds.Add(p.ItemID);
            s.damage = Mathf.Min(s.damage, s.MaxHull - 1);
            Changed?.Invoke();
            return true;
        }

        public bool RemovePart(ShipRecord s, ShipSlot slot)
        {
            if (s == null || !ships.Contains(s) || s.OnCargoRun) return false;
            var p = s.PartIn(slot);
            if (p == null) return false;
            s.partIds.Remove(p.ItemID);
            spareParts.Add(p.ItemID);
            s.damage = Mathf.Min(s.damage, s.MaxHull - 1);
            Changed?.Invoke();
            return true;
        }

        // ---------- Repairs and cargo runs ----------

        public int RepairCost(ShipRecord s) => s != null && s.Definition != null ? s.damage * s.Definition.RepairCostPerPoint : 0;

        public bool Repair(ShipRecord s)
        {
            if (s == null || s.damage <= 0 || s.OnCargoRun || !Spend(RepairCost(s))) return false;
            s.damage = 0;
            Changed?.Invoke();
            return true;
        }

        public bool CanStartCargoRun(ShipRecord s, out string reason)
        {
            reason = null;
            if (s == null || !ships.Contains(s)) reason = "No ship";
            else if (s.OnCargoRun) reason = "Already on a run";
            else if (s.HullFraction < minCargoHull) reason = "Hull too damaged";
            return reason == null;
        }

        public bool StartCargoRun(ShipRecord s)
        {
            if (!CanStartCargoRun(s, out _)) return false;
            s.cargoPay = s.CargoRunPay;
            s.cargoEndsTicks = DateTime.UtcNow.Add(s.CargoRunTime).Ticks;
            Changed?.Invoke();
            return true;
        }

        //Back from a run: the pay comes in and the hull takes some wear. Returns the pay (0 if not back yet).
        public int CollectCargo(ShipRecord s)
        {
            if (s == null || !s.CargoDone) return 0;
            int pay = s.cargoPay;
            credits += pay;
            var d = s.Definition;
            if (d != null) s.damage = Mathf.Min(s.MaxHull - 1, s.damage + Mathf.RoundToInt(s.MaxHull * UnityEngine.Random.Range(0f, d.CargoRunWear)));
            s.cargoEndsTicks = 0;
            s.cargoPay = 0;
            Changed?.Invoke();
            return pay;
        }

        // ---------- Businesses ----------

        public static List<BusinessDefinition> BusinessesForSale()
        {
            var list = new List<BusinessDefinition>(CatalogAsset.All<BusinessDefinition>());
            list.Sort((a, b) => a.Price.CompareTo(b.Price));
            return list;
        }

        public BusinessRecord GetBusiness(BusinessDefinition d)
        {
            if (d != null) foreach (var b in businesses) if (b.definitionId == d.ItemID) return b;
            return null;
        }

        public bool CanBuyBusiness(BusinessDefinition d, out string reason)
        {
            reason = null;
            if (d == null) reason = "Nothing chosen";
            else if (GetBusiness(d) != null) reason = "Already yours";
            else if (!CanAfford(d.Price)) reason = "Needs " + d.Price.ToString("N0") + " credits";
            return reason == null;
        }

        public bool BuyBusiness(BusinessDefinition d)
        {
            if (!CanBuyBusiness(d, out _)) return false;
            credits -= d.Price;
            businesses.Add(new BusinessRecord(d));
            Changed?.Invoke();
            return true;
        }

        public bool CanUpgradeBusiness(BusinessRecord b, out string reason)
        {
            reason = null;
            var d = b != null ? b.Definition : null;
            if (d == null || !businesses.Contains(b)) reason = "Not yours";
            else if (b.level >= d.MaxLevel) reason = "Top level";
            else if (!CanAfford(d.UpgradeCost(b.level))) reason = "Needs " + d.UpgradeCost(b.level).ToString("N0") + " credits";
            return reason == null;
        }

        //Income so far is banked at the old rate before the level goes up
        public bool UpgradeBusiness(BusinessRecord b)
        {
            if (!CanUpgradeBusiness(b, out _)) return false;
            int cost = b.Definition.UpgradeCost(b.level);
            credits -= cost;
            b.Accrue();
            b.level++;
            b.spent += cost;
            Changed?.Invoke();
            return true;
        }

        public int CollectBusiness(BusinessRecord b)
        {
            if (b == null || !businesses.Contains(b)) return 0;
            int ready = b.Ready;
            if (ready <= 0) return 0;
            credits += ready;
            b.stored -= ready;
            Changed?.Invoke();
            return ready;
        }

        public int SalePrice(BusinessRecord b) => b != null ? Mathf.RoundToInt(b.spent * sellBack) : 0;

        //Uncollected income is paid out with the sale
        public bool SellBusiness(BusinessRecord b)
        {
            if (b == null || !businesses.Remove(b)) return false;
            credits += SalePrice(b) + b.Ready;
            Changed?.Invoke();
            return true;
        }

        //Every business pays its per-mission income (called by CompleteMission). Returns the total.
        int PayBusinesses()
        {
            int total = 0;
            foreach (var b in businesses)
                if (b.Definition != null) total += b.Definition.IncomePerMission(b.level);
            credits += total;
            return total;
        }

        // ---------- Saving ----------

        void CaptureFleet(CompanyState s)
        {
            s.ships = new List<ShipRecord>(ships);
            s.businesses = new List<BusinessRecord>(businesses);
            s.spareParts = new List<string>(spareParts);
            s.activeShipId = activeShipId;
        }

        void RestoreFleet(CompanyState s)
        {
            ships.Clear(); if (s.ships != null) ships.AddRange(s.ships);
            businesses.Clear(); if (s.businesses != null) businesses.AddRange(s.businesses);
            spareParts.Clear(); if (s.spareParts != null) spareParts.AddRange(s.spareParts);
            activeShipId = s.activeShipId;
        }
    }
}
