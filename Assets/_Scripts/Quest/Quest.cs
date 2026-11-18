using System;
using System.Collections.Generic;
using Rambler.Combat;
using UnityEngine;

namespace Rambler.Quests
{
    //A quest is an ordered list of objectives. The objective HUD shows the first one not yet done.
    //Quest assets must sit in a Resources folder (e.g. _Scripts/QuestSO/Resources) so saves and dialogue
    //conditions can find them by name.
    [CreateAssetMenu(fileName = "Quest", menuName = "RPG Project/Quest", order = 0)]
    public class Quest : ScriptableObject {
        [Tooltip("Name shown on screen. Left empty, the asset's file name is used.")]
        [SerializeField] string displayName = "";
        [Tooltip("On: objectives must be done top to bottom; completing a later one early is ignored. Off: any order (the HUD still shows the first unfinished one).")]
        [SerializeField] bool inOrder = true;
        [SerializeField] List<Objective> objectives = new List<Objective>();
        [SerializeField] List<Reward> rewards = new List<Reward>();

        [System.Serializable]
        public class Reward
        {
            [Min(1)]
            public int number;
            public Weapon weaponConfig;
        }

        [System.Serializable]
        public class Objective
        {
            [Tooltip("ID used by triggers and dialogue to complete this objective, e.g. \"find-beacon\"")]
            public string reference;
            [Tooltip("Text shown on screen, e.g. \"Find the crash site beacon\"")]
            public string description;
        }

        static Dictionary<string, Quest> lookup;

        public string GetTitle()
        {
            return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        }

        public bool IsInOrder()
        {
            return inOrder;
        }

        public int GetObjectiveCount()
        {
            return objectives.Count;
        }

        public IEnumerable<Objective> GetObjectives()
        {
            return objectives;
        }

        public IEnumerable<Reward> GetRewards()
        {
            return rewards;
        }

        public bool HasObjective(string objectiveRef)
        {
            foreach (var objective in objectives)
            {
                if (objective.reference == objectiveRef)
                {
                    return true;
                }
            }
            return false;
        }

        //Looks a quest up by asset name. Loads the Resources folders once, then uses the cache.
        public static Quest GetByName(string questName)
        {
            if (string.IsNullOrEmpty(questName)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, Quest>();
                foreach (Quest quest in Resources.LoadAll<Quest>(""))
                {
                    if (lookup.ContainsKey(quest.name))
                    {
                        Debug.LogError("Two quests share the name " + quest.name + "; saves can only find one of them");
                        continue;
                    }
                    lookup[quest.name] = quest;
                }
            }
            lookup.TryGetValue(questName, out Quest found);
            if (found == null) Debug.LogWarning("Quest " + questName + " not found. Is it in a Resources folder?");
            return found;
        }

#if UNITY_EDITOR
        //Domain reload can be off in the editor, so clear the cache on each play
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearLookup() => lookup = null;
#endif
    }
}
