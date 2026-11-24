using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Crew
{
    //Where a ship part fits. Every ship has one of each.
    public enum ShipSlot { Engine, Hull, Cargo, Bay, Weapons, Utility }

    //What a ship can do for the squad in a mission (used from the Command/Map screen, phase 5b)
    public enum SupportAbility { None, Resupply, Scan, Strike, Extraction }

    //A ship's numbers. A ShipDefinition gives the base (seats, range, hull); parts add to them.
    //cargoPay and cargoTime are percents (cargoTime -20 = runs finish 20% sooner).
    [Serializable]
    public struct ShipStats
    {
        [Tooltip("Crew the ship carries into a mission.")]
        public int crewSeats;
        [Tooltip("How far it can fly: places further out need more range.")]
        public int range;
        [Tooltip("Hull points. Damage comes from cargo runs (and later from missions).")]
        public int hull;
        [Tooltip("Power pips the Engines station shares out between Navigation, Weapons and Systems in a ship battle.")]
        public int power;
        [Tooltip("Damage of each shot from the Weapons station.")]
        public int firepower;
        [Tooltip("Percent more pay from cargo runs.")]
        public float cargoPay;
        [Tooltip("Percent change in cargo run time (negative is faster).")]
        public float cargoTime;

        public bool IsEmpty => crewSeats == 0 && range == 0 && hull == 0 && power == 0 && firepower == 0 && cargoPay == 0f && cargoTime == 0f;

        public static ShipStats operator +(ShipStats a, ShipStats b) => new ShipStats
        {
            crewSeats = a.crewSeats + b.crewSeats,
            range = a.range + b.range,
            hull = a.hull + b.hull,
            power = a.power + b.power,
            firepower = a.firepower + b.firepower,
            cargoPay = a.cargoPay + b.cargoPay,
            cargoTime = a.cargoTime + b.cargoTime,
        };

        //A part's bonuses as short text, e.g. "+1 seat  +150 hull  -20% run time"
        public string Describe()
        {
            var parts = new List<string>();
            if (crewSeats != 0) parts.Add(Signed(crewSeats) + (Mathf.Abs(crewSeats) == 1 ? " seat" : " seats"));
            if (range != 0) parts.Add(Signed(range) + " range");
            if (hull != 0) parts.Add(Signed(hull) + " hull");
            if (power != 0) parts.Add(Signed(power) + " power");
            if (firepower != 0) parts.Add(Signed(firepower) + " firepower");
            if (cargoPay != 0f) parts.Add(Signed(cargoPay) + "% cargo pay");
            if (cargoTime != 0f) parts.Add(Signed(cargoTime) + "% run time");
            return string.Join("  ", parts);
        }

        static string Signed(float v) => (v > 0 ? "+" : "") + v.ToString("0.#");
    }
}
