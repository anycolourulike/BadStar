using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Rambler.Control;
using Rambler.Saving;

namespace Rambler.Crew
{
    //Puts hired crew into a mission scene as playable squad members: spawns each one's prefab at a spawn point,
    //binds it to its record, and hands it to the PlayerSelector (which caps the squad at MaxPlayers).
    public class SquadDeployer : MonoBehaviour
    {
        [SerializeField] PlayerSelector playerSelector;
        [Tooltip("Where crew appear, in order. Empty = around this object.")]
        [SerializeField] Transform[] spawnPoints = new Transform[0];
        [Tooltip("Deploy the first crew who are free when the scene starts.")]
        [SerializeField] bool deployOnStart = true;
        [SerializeField] float spacing = 1.5f;

        readonly List<RosterMember> deployed = new List<RosterMember>();

        public IReadOnlyList<RosterMember> Deployed => deployed;

        void Start()
        {
            if (playerSelector == null) playerSelector = FindObjectOfType<PlayerSelector>();
            if (deployOnStart) DeployAvailable();
        }

        //Everyone free, until the squad is full
        public void DeployAvailable()
        {
            var company = Company.Instance;
            if (company == null) return;
            foreach (var r in new List<CharacterRecord>(company.Crew))
            {
                if (playerSelector != null && playerSelector.IsFull) break;
                if (r.CanDeploy) Deploy(r);
            }
        }

        public RosterMember Deploy(CharacterRecord record)
        {
            if (record == null || !record.CanDeploy) return null;
            if (playerSelector == null) playerSelector = FindObjectOfType<PlayerSelector>(); //Deploy can come before Start
            if (playerSelector == null) { Debug.LogWarning("[SquadDeployer] No PlayerSelector in the scene."); return null; }
            if (playerSelector.IsFull) { Debug.Log("[SquadDeployer] Squad is full; " + record.displayName + " stays behind."); return null; }
            var definition = record.Definition;
            if (definition == null || definition.Prefab == null) { Debug.LogWarning("[SquadDeployer] " + record.displayName + " has no Character definition or prefab."); return null; }

            GetSpawn(deployed.Count, out Vector3 position, out Quaternion rotation);

            //Spawn under an inactive holder so nothing wakes before the record is applied
            var holder = new GameObject("Deploying");
            holder.SetActive(false);
            var go = Instantiate(definition.Prefab, position, rotation, holder.transform);

            //The record is the save for this character; a scene save ID on a spawned copy would collide
            foreach (var entity in go.GetComponentsInChildren<SaveableEntity>(true)) Destroy(entity);

            go.tag = "Companion";
            var member = go.AddComponent<RosterMember>();
            member.Bind(record);

            //A companion until the player switches to them (PlayerSelector does the same in ControlPlayer)
            var pc = go.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
            var brain = go.GetComponent<CompanionBrain>();
            if (brain != null) brain.enabled = true;

            go.transform.SetParent(null, true);
            Destroy(holder);

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(position);

            if (!playerSelector.AddPlayer(go))
            {
                Destroy(go);
                return null;
            }
            Company.Instance.MarkDeployed(record);
            deployed.Add(member);
            return member;
        }

        void GetSpawn(int index, out Vector3 position, out Quaternion rotation)
        {
            if (spawnPoints.Length > 0 && spawnPoints[index % spawnPoints.Length] != null)
            {
                var p = spawnPoints[index % spawnPoints.Length];
                position = p.position + p.right * spacing * (index / spawnPoints.Length);
                rotation = p.rotation;
            }
            else
            {
                position = transform.position + transform.right * spacing * index;
                rotation = transform.rotation;
            }
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) position = hit.position;
        }
    }
}
