using System;
using UnityEngine;

namespace Rambler.Combat
{
    //Percent changes an upgrade, attachment, gear item or skill level makes (0 = no change; -25 = 25% less).
    [Serializable]
    public struct ModifierSet
    {
        [Tooltip("Damage of each shot.")] public float damage;
        [Tooltip("Spread (negative = more accurate).")] public float spread;
        [Tooltip("Muzzle climb per shot (negative = steadier).")] public float kick;
        [Tooltip("Shots per second.")] public float fireRate;
        [Tooltip("Magazine size.")] public float magazine;
        [Tooltip("Damage taken (negative = armour).")] public float damageTaken;
        [Tooltip("Maximum health.")] public float health;
        [Tooltip("Movement speed.")] public float speed;

        public static ModifierSet operator +(ModifierSet a, ModifierSet b) => new ModifierSet
        {
            damage = a.damage + b.damage, spread = a.spread + b.spread, kick = a.kick + b.kick, fireRate = a.fireRate + b.fireRate,
            magazine = a.magazine + b.magazine, damageTaken = a.damageTaken + b.damageTaken, health = a.health + b.health, speed = a.speed + b.speed,
        };

        public static ModifierSet operator *(ModifierSet a, float k) => new ModifierSet
        {
            damage = a.damage * k, spread = a.spread * k, kick = a.kick * k, fireRate = a.fireRate * k,
            magazine = a.magazine * k, damageTaken = a.damageTaken * k, health = a.health * k, speed = a.speed * k,
        };

        public bool IsEmpty => damage == 0 && spread == 0 && kick == 0 && fireRate == 0 && magazine == 0 && damageTaken == 0 && health == 0 && speed == 0;

        //"Damage +10%  Spread -20%" for the UI
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            void Add(string label, float v) { if (Mathf.Abs(v) < 0.05f) return; if (sb.Length > 0) sb.Append("   "); sb.Append(label).Append(v > 0 ? " +" : " ").Append(v.ToString("0.#")).Append('%'); }
            Add("Damage", damage); Add("Spread", spread); Add("Recoil", kick); Add("Fire rate", fireRate);
            Add("Magazine", magazine); Add("Damage taken", damageTaken); Add("Health", health); Add("Speed", speed);
            return sb.ToString();
        }
    }

    //The multipliers a character's gear, weapon attachments and skills add up to. Fighter, ActiveWeapon and Health
    //read them; characters without this component (enemies) use the plain weapon and take full damage.
    //Set by RosterMember for hired crew.
    [DisallowMultipleComponent]
    public class CombatModifiers : MonoBehaviour
    {
        [SerializeField] ModifierSet total;

        public ModifierSet Total => total;
        public float Damage => Mult(total.damage);
        public float Spread => Mult(total.spread);
        public float Kick => Mult(total.kick);
        public float FireRate => Mult(total.fireRate);
        public float Magazine => Mult(total.magazine);
        public float DamageTaken => Mult(total.damageTaken);
        public float Speed => Mult(total.speed);

        public event Action Changed;

        public void Set(ModifierSet value)
        {
            total = value;
            Changed?.Invoke();
        }

        static float Mult(float percent) => Mathf.Max(0.1f, 1f + percent / 100f);

        //Multipliers for anyone; 1 for characters without the component
        public static float DamageOf(GameObject g) => Get(g, m => m.Damage);
        public static float SpreadOf(GameObject g) => Get(g, m => m.Spread);
        public static float KickOf(GameObject g) => Get(g, m => m.Kick);
        public static float FireRateOf(GameObject g) => Get(g, m => m.FireRate);
        public static float MagazineOf(GameObject g) => Get(g, m => m.Magazine);
        public static float DamageTakenOf(GameObject g) => Get(g, m => m.DamageTaken);

        static float Get(GameObject g, Func<CombatModifiers, float> pick)
        {
            if (g == null) return 1f;
            var m = g.GetComponent<CombatModifiers>();
            return m != null ? pick(m) : 1f;
        }
    }
}
