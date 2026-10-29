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
using Rambler.Saving;
using TMPro;

//Track number of dead enemies
//Track mission pick ups status
//Track mission story with Level Manager
namespace Rambler.Control
{
    public class PlayerSelector : MonoBehaviour, ISaveable
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
        [SerializeField] TextMeshProUGUI displayLives;

        [SerializeField] PlayerController activePlayerCon;
        [SerializeField] AIController companionAI;  

        public InventoryUI UiInventory;
        public Inventory inv;
        public int lives;

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
            activePlayerAI.enabled = false;

            activePlayerCon = activePlayer.GetComponent<PlayerController>();
            activePlayerCon.enabled = true;
            
            UpdatePlayerObject(activePlayer);
            foreach(GameObject character in players)
            {
                if(character != player)
                {
                    companion = character;
                    companion.tag = "Companion";
                    var companionAI = companion.GetComponent<AIController>();
                    companionAI.enabled = true;
                    companionAI.IsPatrolling();
                    var companionPlayerCon = companion.GetComponent<PlayerController>();
                    companionPlayerCon.enabled = false;                    
                }
            }            
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

        public void StartingLives()
        {
            //check if lives are infinite
            lives = 5;
            displayLives.text = lives.ToString();
        }

        public void LivesUpdate()
        {
            displayLives.text = lives.ToString();
            //if infinite set UI to disabled
        }

        public void AddOneLife()
        {
            lives++;
        }

        void PlayerDeath(GameObject player)
        {
            players.Remove(player);
            PlayersChanged?.Invoke();
            if(players.Count == 0)
            {
                //Lose a life
                //if (infiniteLives == true) return;
                //lives--;
                //LevelManager.Instance.lifeWasLost = true;
                //displayLives.text = lives.ToString();
                //SavingWrapper wrapper = FindObjectOfType<SavingWrapper>();
                //healthPoints = new LazyValue<float>(GetInitialHealth);
                //wrapper.Save();
            }
            else
            {
                var newCharacter = players[0];
                ControlPlayer(newCharacter);
            }     
        }
      
        void UpdateInventory()
        {
            inv = activePlayer.GetComponent<Inventory>();
            UiInventory.playerInventory = inv;
            UiInventory.activePlayer = activePlayer;
            UiInventory.Redraw();
        }

        public object CaptureState()
        {
            //Debug.Log(this.gameObject.name + " " + HealthPoints.value);
            Dictionary<string, object> data = new Dictionary<string, object>();           
            data["lives"] = lives;            
            return data;
        }

        public void RestoreState(object state)
        {
            Dictionary<string, object> data = (Dictionary<string, object>)state;
            lives = (int)data["lives"]; //∞
            LivesUpdate();
        }
    }
}
