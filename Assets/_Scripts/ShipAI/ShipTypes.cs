using UnityEngine;
using Rambler.Attributes;

namespace Rambler.Ship
{
    //Who the ship works for. It starts Hostile; hacking terminals hands control to the player
    public enum ShipFaction
    {
        Hostile,
        Player
    }

    public enum AlertState
    {
        Dormant,     //Nothing seen
        Suspicious,  //Brief sightings; sensors glow amber
        Alert,       //Player confirmed; the ship starts sealing and crushing
        Lockdown     //Alert held too long; every door seals
    }

    //Ship systems a terminal hands to the player. The ship stops using a system once the player has it;
    //when every terminal is hacked the whole ship changes sides.
    public enum ShipControl
    {
        Doors,        //Seal, open and crush doors
        LifeSupport,  //Vent oxygen, freeze and restore rooms
        Sensors       //Blind the ship and see who is in each room
    }

    //Shared lookups for working out which character a collider belongs to and whose side it is on
    public static class ShipCharacters
    {
        public static GameObject Root(Collider collider)
        {
            var health = collider.GetComponentInParent<Health>();
            return health != null ? health.gameObject : null;
        }

        public static bool IsPlayerSide(GameObject character)
        {
            return character.CompareTag("Player") || character.CompareTag("Companion");
        }

        public static bool IsAlive(GameObject character)
        {
            if (character == null) return false;
            var health = character.GetComponent<Health>();
            return health != null && !health.IsDead();
        }
    }
}
