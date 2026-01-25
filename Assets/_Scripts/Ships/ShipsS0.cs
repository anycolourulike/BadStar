using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "newShip", menuName = "Ships")]
public class ShipsSO: ScriptableObject
{
    // Define variables that you want to store in the Scriptable Object
    public int laser;
    public int storage;   
    public int rockets;
    
    public float fuel;
    public float shield;
    public string shipName;
    public enum Shiptype
    {
        freighter,
        fighter,
        frigate
    }
   
    // Add any other variables as needed
}
