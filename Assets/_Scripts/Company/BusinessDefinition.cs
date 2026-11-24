using UnityEngine;
using Rambler.Ownables;

namespace Rambler.Crew
{
    //A business the Company can buy from a Broker. It pays every completed mission and also builds up income in
    //real time (even while the game is closed) up to a cap, collected at the Broker. Levels raise both.
    //One of each can be owned. Keep these in a Resources folder; every business there is for sale.
    [CreateAssetMenu(fileName = "Business", menuName = "Rambler/Company/Business")]
    public class BusinessDefinition : OwnableDefinition
    {
        [Tooltip("Paid into the Company each time a mission is completed (level 1).")]
        [Min(0)] [SerializeField] int incomePerMission = 50;
        [Tooltip("Credits built up per real-time hour (level 1).")]
        [Min(0)] [SerializeField] int incomePerHour = 60;
        [Tooltip("Hours of income it holds before it stops building up; collect it at a Broker.")]
        [Min(0.1f)] [SerializeField] float storeHours = 8f;
        [Min(1)] [SerializeField] int maxLevel = 5;
        [Tooltip("Percent more income for each level above 1.")]
        [SerializeField] float incomePerLevel = 50f;
        [Tooltip("Cost of going from level 1 to 2; each later level costs this times the current level.")]
        [Min(0)] [SerializeField] int upgradeCost = 1000;

        public int MaxLevel => maxLevel;
        public float StoreHours => storeHours;

        public float Multiplier(int level) => 1f + Mathf.Max(0, level - 1) * incomePerLevel / 100f;
        public int IncomePerMission(int level) => Mathf.RoundToInt(incomePerMission * Multiplier(level));
        public float IncomePerHour(int level) => incomePerHour * Multiplier(level);
        public int Capacity(int level) => Mathf.RoundToInt(IncomePerHour(level) * storeHours);
        public int UpgradeCost(int level) => upgradeCost * Mathf.Max(1, level);
    }
}
