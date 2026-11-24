using System;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //One ship the Company owns: which ShipDefinition it is, the parts fitted, its damage and any cargo run.
    //Saved with the Company. Times are real-world UTC ticks so cargo runs carry on while the game is closed.
    [Serializable]
    public class ShipRecord
    {
        public string recordId = Guid.NewGuid().ToString();
        public string definitionId;
        public List<string> partIds = new List<string>();
        public int damage;
        public long cargoEndsTicks;   //0 = docked
        public int cargoPay;

        public ShipRecord() { }
        public ShipRecord(ShipDefinition d) { definitionId = d.ItemID; }

        public ShipDefinition Definition => CatalogAsset.GetFromID<ShipDefinition>(definitionId);
        public string DisplayName => Definition != null ? Definition.DisplayName : "Unknown ship";

        public ShipPartDefinition PartIn(ShipSlot slot)
        {
            foreach (var id in partIds)
            {
                var p = CatalogAsset.GetFromID<ShipPartDefinition>(id);
                if (p != null && p.Slot == slot) return p;
            }
            return null;
        }

        //Base numbers plus every fitted part
        public ShipStats Stats
        {
            get
            {
                var s = Definition != null ? Definition.BaseStats : new ShipStats();
                foreach (var id in partIds)
                {
                    var p = CatalogAsset.GetFromID<ShipPartDefinition>(id);
                    if (p != null) s += p.Stats;
                }
                return s;
            }
        }

        public List<SupportAbility> Abilities
        {
            get
            {
                var list = new List<SupportAbility>();
                foreach (var id in partIds)
                {
                    var p = CatalogAsset.GetFromID<ShipPartDefinition>(id);
                    if (p != null && p.Ability != SupportAbility.None && !list.Contains(p.Ability)) list.Add(p.Ability);
                }
                return list;
            }
        }

        public int MaxHull => Mathf.Max(1, Stats.hull);
        public int Hull => Mathf.Max(0, MaxHull - damage);
        public float HullFraction => (float)Hull / MaxHull;

        public bool OnCargoRun => cargoEndsTicks != 0;
        public bool CargoDone => OnCargoRun && DateTime.UtcNow.Ticks >= cargoEndsTicks;
        public TimeSpan CargoTimeLeft => OnCargoRun ? TimeSpan.FromTicks(Math.Max(0, cargoEndsTicks - DateTime.UtcNow.Ticks)) : TimeSpan.Zero;

        //What a cargo run would pay and how long it would take, with the fitted parts
        public int CargoRunPay => Definition != null ? Mathf.RoundToInt(Definition.CargoRunPay * Mathf.Max(0f, 1f + Stats.cargoPay / 100f)) : 0;
        public TimeSpan CargoRunTime => TimeSpan.FromMinutes(Definition != null ? Definition.CargoRunMinutes * Mathf.Max(0.1f, 1f + Stats.cargoTime / 100f) : 0f);
    }
}
