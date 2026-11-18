using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;
using Rambler.Combat;
using Rambler.Attributes;
using Rambler.UI;
using Rambler.Inventories;
using Rambler.UI.Inventories;
using System;

//Track number of dead enemies
//Track mission pick ups status
//Track mission story with Level Manager
namespace Rambler.Control
{
    public class PlayerSelector : MonoBehaviour
    {
        public delegate void PlayerObjectChanged(GameObject newPlayer);
        public static event PlayerObjectChanged OnPlayerObjectChanged;

        //No more than this many units under the player's control at once (one HUD portrait each)
        public const int MaxPlayers = 4;
        //Raised when a unit joins or leaves the player's side
        public event Action PlayersChanged;

        public CameraManager camMan;

        public GameObject activePlayer;        
        public GameObject companion;

        public ShowHideUI showHideUI;
        public List<GameObject> players = new();

        [SerializeField] PlayerController activePlayerCon;
        [SerializeField] AIController companionAI;  

        public InventoryUI UiInventory;
        public Inventory inv;

        private void OnEnable()
        {
            Health.playerDeath += PlayerDeath;
            Health.companionDeath += PlayerDeath;
        }       

        public void OnDisable()
        {
            Health.playerDeath -= PlayerDeath;
            Health.companionDeath -= PlayerDeath;
        }       

        void Start()
        {            
            UpdateInventory();
            ControlPlayer(activePlayer);
        }        

        public static void UpdatePlayerObject(GameObject newPlayerObject)
        {
            OnPlayerObjectChanged?.Invoke(newPlayerObject);
        }

        public void ControlPlayer(GameObject player)
        {
            //Both the outgoing and incoming characters must be in the list, or the old one stays tagged Player
            AddPlayer(activePlayer);
            AddPlayer(player);
            activePlayer = player;
            showHideUI.HideInv(activePlayer);            
            camMan.UpdateFollow(activePlayer.transform);
            UpdateInventory();

            activePlayer.tag = "Player";
            var activePlayerAI = activePlayer.GetComponent<AIController>();
            if (activePlayerAI != null) activePlayerAI.enabled = false;

            activePlayerCon = activePlayer.GetComponent<PlayerController>();
            activePlayerCon.enabled = true;

            foreach(GameObject character in players)
            {
                if(character != player && character != null)
                {
                    companion = character;
                    companion.tag = "Companion";
                    //Companions on the behaviour tree (CompanionBrain) start it themselves from the event below;
                    //the rest still run the old AIController
                    var brain = companion.GetComponent<CompanionBrain>();
                    if (brain != null) brain.enabled = true;
                    else
                    {
                        var companionAI = companion.GetComponent<AIController>();
                        if (companionAI != null)
                        {
                            companionAI.enabled = true;
                            companionAI.IsPatrolling();
                        }
                    }
                    var companionPlayerCon = companion.GetComponent<PlayerController>();
                    if (companionPlayerCon != null) companionPlayerCon.enabled = false;
                }
            }
            //After the tags are set, so listeners see who is Player and who is Companion
            UpdatePlayerObject(activePlayer);
        }

        public bool IsFull => players.Count >= MaxPlayers;

        //A character you can switch to (e.g. a hacked robot). Safe to call more than once.
        //Returns false when the squad is already at MaxPlayers.
        public bool AddPlayer(GameObject character)
        {
            if (character == null) return false;
            if (players.Contains(character)) return true;
            if (IsFull) return false;
            players.Add(character);
            PlayersChanged?.Invoke();
            return true;
        }

        //Take control of one of your characters; ignored if they are dead or not yours
        public void SwitchTo(GameObject character)
        {
            if (character == null || character == activePlayer || !players.Contains(character)) return;
            var health = character.GetComponent<Health>();
            if (health != null && health.IsDead()) return;
            ControlPlayer(character);
        }

        //Called by the HUD portraits
        public void ActivateRambler() { SwitchTo(FindPlayer("Rambler")); }
        public void ActivateHayes() { SwitchTo(FindPlayer("Hayes")); }

        GameObject FindPlayer(string characterName)
        {
            foreach (var character in players)
            {
                if (character != null && character.name == characterName) return character;
            }
            return null;
        }

        public int PlayersCheck()
        {
            if (players != null)
            {
                return players.Count;
            }
            else
            {
                Debug.LogError("Players list is not initialized!");
                return 0;
            }
        }

        public void DisplayYouDied()
        {

        }

        public void DisplayGameOver()
        {

        }

        public void InteractBtnPressed()
        {
            activePlayerCon.InteractPressed();
        }

        public void ReloadActiveWeapon()
        {
            activePlayerCon.ReloadActiveWeapon();
        }

        public void ActivateCrouch()
        {
            activePlayerCon.ToggleCrouch();
        }

        public void ActivateShields()
        {
            activePlayerCon.ToggleShields();
        }       

        void PlayerDeath(GameObject player)
        {
            bool wasActive = player == activePlayer;
            players.Remove(player);
            PlayersChanged?.Invoke();
            //Only take the player off the character they're using if that one died
            if (wasActive && players.Count > 0) ControlPlayer(players[0]);
        }
      
        void UpdateInventory()
        {
            inv = activePlayer.GetComponent<Inventory>();
            UiInventory.SetInventory(activePlayer);
        }
    }
}
