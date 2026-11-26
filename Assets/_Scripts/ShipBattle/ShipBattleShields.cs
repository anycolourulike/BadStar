using UnityEngine;

namespace Rambler.Crew.Battle
{
    //Shields (the ShipBattle partial), FTL style. Each layer stops one incoming hit outright, then recharges.
    //  Ours: a power row of their own on the Engines screen (two pips per layer, up to four); the shield generator
    //    is in the Systems room, so whoever is at Ship Systems recharges them, faster with skill. Systems damaged,
    //    EMP'd or taken by boarders: the shields drop and stay down.
    //  Theirs: up to two layers. A layer stops a rocket, but missiles pierce shields. Hitting their Shields room
    //    knocks the generator out for a while. A scan shows how many layers are up.
    //Both ships show their shields as rings around their lights on the tactical map.
    public partial class ShipBattle
    {
        [Header("Shields")]
        [Tooltip("Power pips per shield layer.")]
        [SerializeField] int pipsPerLayer = 2;
        [Tooltip("Seconds to recharge one layer with Ship Systems at 100%.")]
        [SerializeField] float shieldRechargeSeconds = 5f;
        [SerializeField] int enemyShieldMax = 2;
        [SerializeField] float enemyShieldRechargeSeconds = 7f;

        int shieldPips;
        float enemyShieldDown;

        //Layers up right now; the fraction is the next one charging
        public float ShieldCharge { get; private set; }
        public int ShieldLayers => Mathf.FloorToInt(ShieldCharge + 0.0001f);
        public int ShieldMax => ShieldWorking ? Mathf.Min(shieldPips, maxPips) / Mathf.Max(1, pipsPerLayer) : 0;
        public int ShieldPips => shieldPips;
        public int PipsPerLayer => pipsPerLayer;
        //The generator runs off Ship Systems: damaged, EMP'd or taken, it's down
        public bool ShieldWorking => CrewFactor(Station.Systems) > 0f;

        public float EnemyShieldCharge { get; private set; }
        public int EnemyShieldLayers => Mathf.FloorToInt(EnemyShieldCharge + 0.0001f);
        public int EnemyShieldMax => EnemyShieldsDown ? 0 : enemyShieldMax;
        public int EnemyShields { get; private set; }
        public bool EnemyShieldsDown => enemyShieldDown > 0f;

        public bool AddShieldPip()
        {
            if (!CanChangePower || FreePips <= 0 || shieldPips >= maxPips) return false;
            shieldPips++;
            Changed?.Invoke();
            return true;
        }

        public bool RemoveShieldPip()
        {
            if (!CanChangePower || shieldPips <= 0) return false;
            shieldPips--;
            Changed?.Invoke();
            return true;
        }

        void ShieldsStart()
        {
            //Start with whatever layers the opening power allows (the pips are set in Start)
            ShieldCharge = ShieldMax;
            EnemyShields = enemySystemHealth;
            EnemyShieldCharge = enemyShieldMax;
        }

        void ShieldsUpdate(float dt)
        {
            int max = ShieldMax;
            if (ShieldCharge < max) ShieldCharge = Mathf.Min(max, ShieldCharge + CrewFactor(Station.Systems) * dt / Mathf.Max(0.5f, shieldRechargeSeconds));
            else if (ShieldCharge > max) ShieldCharge = max;

            if (enemyShieldDown > 0f && (enemyShieldDown -= dt) <= 0f) { EnemyShields = enemySystemHealth / 2; Log("The " + enemyName + "'s shields are back online."); }
            int emax = EnemyShieldMax;
            if (EnemyShieldCharge < emax) EnemyShieldCharge = Mathf.Min(emax, EnemyShieldCharge + dt / Mathf.Max(0.5f, enemyShieldRechargeSeconds));
            else if (EnemyShieldCharge > emax) EnemyShieldCharge = emax;

            if (HasTacticalMap)
            {
                space.playerShip.ShowShield(ShieldLayers, ShieldCharge);
                space.enemyShip.ShowShield(EnemyShieldLayers, EnemyShieldCharge);
            }
        }

        //An enemy shot that would hit: a layer takes it instead
        bool OurShieldsStop()
        {
            if (ShieldLayers < 1) return false;
            ShieldCharge -= 1f;
            if (HasTacticalMap) space.playerShip.FlashShield();
            Log("<color=#4CA6F2>Shields took the hit.</color> " + ShieldLayers + " layer" + (ShieldLayers == 1 ? "" : "s") + " left.");
            return true;
        }

        //One of our shots that would hit: a rocket is stopped by a layer; a missile goes through
        bool EnemyShieldsStop(Shot shot)
        {
            if (shot.pierce || EnemyShieldLayers < 1) return false;
            EnemyShieldCharge -= 1f;
            if (HasTacticalMap) space.enemyShip.FlashShield();
            Log("The " + enemyName + "'s shields stopped a rocket" + (EnemyShieldLayers > 0 ? "." : ": their shields are down!"));
            return true;
        }

        //A hit on their Shields room
        void DamageEnemyShields(int dmg)
        {
            EnemyHull -= dmg / 2;
            if (!EnemyShieldsDown && (EnemyShields -= dmg) <= 0)
            {
                EnemyShields = 0; enemyShieldDown = systemDownSeconds; EnemyShieldCharge = 0f;
                Log("The " + enemyName + "'s shield generator is knocked out!");
            }
            else Log("Hit the " + enemyName + "'s shield generator.");
        }
    }
}
