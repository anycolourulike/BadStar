using UnityEngine;
using Missiles;

namespace Rambler.Crew.Battle
{
    //A missile or rocket on the tactical map, flown by the Missile and Rocket Behavior package (BaseMissile: homing
    //turn, acceleration, lifetime, explosion). Instead of hunting anything with a tag, it's locked onto the target
    //the battle gives it: the enemy ship's light for a hit, or a point beside it for a miss. On arrival it reports
    //back (Arrived) and explodes. Rockets are fast with little turn; missiles slower but steer hard.
    public class TacticalMissile : BaseMissile
    {
        //Called once when it reaches its target (or runs out of life)
        public System.Action<TacticalMissile> Arrived;
        //What it carries, for the battle to resolve on arrival
        public object Payload;

        bool reported;

        void Start()
        {
            damageOnTrigger = false;
            damageOnCollision = false;
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            offSet = missileStability;
            currentSpeed = startSpeed;
        }

        //Locked on by the battle: never goes looking for tagged targets
        public override void TargetEnemy() { }
        public override void TargetNearestEnemy(float minDistance, float maxDistance) { }
        public override void TargetRandomEnemy() { }

        public void Launch(GameObject target, float explodeWithin)
        {
            SelectedTarget = target;
            distanceToExplode = explodeWithin;
        }

        void FixedUpdate()
        {
            if (activate) return;
            ExplodeByDistance(distanceToExplode);
            if (activate) return;
            EndLifeByTime();
            if (activate) return;
            PersuitTargetZX();
            Accelerate(accelerationSpeed);
        }

        public override void MissileContact()
        {
            if (!reported) { reported = true; Arrived?.Invoke(this); }
            base.MissileContact();
        }
    }
}
