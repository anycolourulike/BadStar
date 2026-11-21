namespace Rambler.Control
{
    //Presets the Character Setup tool can build. Add new presets here,
    //then create a matching Character Setup Profile asset for each one.
    //A preset is a role (e.g. Miner); what the character is comes from its CharacterType.
    public enum CharacterPreset
    {
        Miner,
        Guard,      //Copies AtlasMK3: guard AI, pistol, cover, weapon rig
        Squad,      //Copies Hayes: companion brain, squad controller
        Civilian    //Copies the Scientist: walks about, no weapon (e.g. a heist target)
    }
}
