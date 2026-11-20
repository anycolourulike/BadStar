namespace Rambler.Control
{
    //Standing orders for the squad members the player isn't controlling. A mission sets these; normal play leaves
    //them off (companions follow the player and fight whatever they see).
    public static class SquadOrders
    {
        //Stay where you were left instead of following the player (each character is put in place for a plan)
        public static bool HoldPosition;
        //Don't open fire on anyone until the alarm goes up (a stealth plan breaks the moment someone shoots)
        public static bool HoldFire;
        //The alarm is up: hold fire no longer applies
        public static bool Alarm;

        public static bool WeaponsFree => !HoldFire || Alarm;

        public static void Reset()
        {
            HoldPosition = false;
            HoldFire = false;
            Alarm = false;
        }

        //Editor play mode can skip the domain reload; start every play session with normal orders
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Reset();
    }
}
