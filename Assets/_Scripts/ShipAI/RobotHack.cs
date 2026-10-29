using System;
using UnityEngine;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Core;
using Rambler.Inventories;

namespace Rambler.Ship
{
    //Turns a hacked enemy robot into one of the player's characters: it fights alongside the player
    //as a Companion, and gets what it needs to be switched to and controlled like Rambler or Hayes.
    public static class RobotHack
    {
        public static event Action<GameObject> Hacked;

        public static bool CanHack(GameObject character)
        {
            if (character == null || !character.CompareTag("Enemy")) return false;
            var identity = character.GetComponent<CharacterIdentity>();
            var health = character.GetComponent<Health>();
            return identity != null && identity.CanBeHacked && health != null && !health.IsDead();
        }

        public static bool Convert(GameObject robot, PlayerSelector selector)
        {
            if (!CanHack(robot)) return false;
            if (selector != null && selector.IsFull)
            {
                Debug.Log("[Hack] Squad full (" + PlayerSelector.MaxPlayers + " units); " + robot.name + " not taken");
                return false;
            }

            //Stop fighting the player and leave the enemy wave
            var fighter = robot.GetComponent<Fighter>();
            if (fighter != null) fighter.CancelTarget();
            var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>();
            if (spawner != null) spawner.RemoveEnemy(robot);

            robot.tag = "Companion";
            robot.layer = LayerMask.NameToLayer("Player");

            var fov = robot.GetComponent<FieldOfView>();
            if (fov != null)
            {
                fov.targetMask = LayerMask.GetMask("Enemy");
                Restart(fov); //Rebuilds its target list for the new side
            }

            AddControlParts(robot, selector);

            //Restart the AI so it picks up the Companion states and targets
            var ai = robot.GetComponent<AIController>();
            if (ai != null)
            {
                Restart(ai);
                ai.RefreshTargets();
                ai.IsPatrolling();
            }

            if (selector != null) selector.AddPlayer(robot);
            Debug.Log("[Hack] " + robot.name + " now fights for the player");
            Hacked?.Invoke(robot);
            return true;
        }

        //Everything PlayerSelector.ControlPlayer needs on a character it switches to
        static void AddControlParts(GameObject robot, PlayerSelector selector)
        {
            if (robot.GetComponent<Inventory>() == null) robot.AddComponent<Inventory>();

            var line = robot.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = robot.AddComponent<LineRenderer>();
                //Borrow the movement-path look from the current player
                var template = selector != null && selector.activePlayer != null ? selector.activePlayer.GetComponent<LineRenderer>() : null;
                if (template != null) line.sharedMaterial = template.sharedMaterial;
                line.positionCount = 0;
                line.enabled = false;
            }

            var controller = robot.GetComponent<PlayerController>();
            if (controller == null) controller = robot.AddComponent<PlayerController>();
            controller.playerSelector = selector;
            controller.enabled = false; //AI drives it until the player switches to it
        }

        static void Restart(Behaviour behaviour)
        {
            behaviour.enabled = false;
            behaviour.enabled = true;
        }
    }
}
