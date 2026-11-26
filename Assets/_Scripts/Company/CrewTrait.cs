namespace Rambler.Crew
{
    //A special talent a crew member is born with (set on their CharacterDefinition). Each makes them much better
    //at one ship station, or at healing in the medbay.
    public enum CrewTrait { Pilot, WeaponsMaster, ShipSystems, Engineer, FieldMedic }

    public static class CrewTraits
    {
        //Skill levels a trait adds at its own station
        public const int StationBonus = 3;
        //How much faster a Field Medic heals people in the medbay
        public const float MedicMultiplier = 2f;

        public static string Name(CrewTrait t)
        {
            switch (t)
            {
                case CrewTrait.WeaponsMaster: return "Weapons Master";
                case CrewTrait.ShipSystems: return "Ship Systems";
                case CrewTrait.FieldMedic: return "Field Medic";
                default: return t.ToString();
            }
        }

        public static string Describe(CrewTrait t)
        {
            switch (t)
            {
                case CrewTrait.Pilot: return "+" + StationBonus + " Piloting at the Navigation station";
                case CrewTrait.WeaponsMaster: return "+" + StationBonus + " Firearms at the Weapons station";
                case CrewTrait.ShipSystems: return "+" + StationBonus + " Engineering at the Ship Systems station";
                case CrewTrait.Engineer: return "+" + StationBonus + " Engineering at the Engines station";
                default: return "Heals people in the medbay twice as fast";
            }
        }

        //The station a trait is for, as the Station enum's index (Navigation 0, Weapons 1, Engines 2, Systems 3; -1 = none)
        public static int StationIndex(CrewTrait t)
        {
            switch (t)
            {
                case CrewTrait.Pilot: return 0;
                case CrewTrait.WeaponsMaster: return 1;
                case CrewTrait.Engineer: return 2;
                case CrewTrait.ShipSystems: return 3;
                default: return -1;
            }
        }
    }
}
