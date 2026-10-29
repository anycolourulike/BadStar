namespace Rambler.Control
{
    //What a character is. Decides what can hurt it and whether it can be hacked;
    //which side it is on comes from its tag (see CharacterRole).
    public enum CharacterType
    {
        Human,      //Breathes: suffocates and freezes
        Robot,      //Hackable by default
        Drone,      //Hackable by default
        AtlasMK3,   //Suited; sealed against vacuum and cold
        AtlasMK5,
        AI          //Ship or system intelligence
    }

    //Which side a character is on, read from its tag
    public enum CharacterRole
    {
        Player,     //The character being controlled
        Companion,  //A player character you can switch to
        Enemy,
        Other
    }
}
