using System;
using UnityEngine;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //One business the Company owns: its level and the income it has built up since it was last collected.
    //Times are real-world UTC ticks so income builds up while the game is closed.
    [Serializable]
    public class BusinessRecord
    {
        public string recordId = Guid.NewGuid().ToString();
        public string definitionId;
        public int level = 1;
        public float stored;
        public long lastTicks;
        public int spent;   //Paid for it and its upgrades, for the sale price

        public BusinessRecord() { }
        public BusinessRecord(BusinessDefinition d)
        {
            definitionId = d.ItemID;
            lastTicks = DateTime.UtcNow.Ticks;
            spent = d.Price;
        }

        public BusinessDefinition Definition => CatalogAsset.GetFromID<BusinessDefinition>(definitionId);
        public string DisplayName => Definition != null ? Definition.DisplayName : "Unknown business";

        //Brings the stored income up to now (capped); a clock set back just restarts the count
        public void Accrue()
        {
            long now = DateTime.UtcNow.Ticks;
            var d = Definition;
            if (d == null || lastTicks == 0 || now < lastTicks) { lastTicks = now; return; }
            double hours = TimeSpan.FromTicks(now - lastTicks).TotalHours;
            stored = Mathf.Min(d.Capacity(level), stored + (float)(hours * d.IncomePerHour(level)));
            lastTicks = now;
        }

        public int Ready { get { Accrue(); return Mathf.FloorToInt(stored); } }
        public bool Full => Definition != null && Ready >= Definition.Capacity(level);
    }
}
