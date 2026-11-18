using System;
using System.Collections.Generic;
using Rambler.Inventories;
using Rambler.Saving;
using Rambler.Utils;
using UnityEngine;

namespace Rambler.Quests
{
    //The player's quests (on Rambler). The newest unfinished quest is the "current" one, and its first unfinished
    //objective is what ObjectiveHUD shows. Givers, completions, triggers and dialogue all come through here.
    public class QuestList : MonoBehaviour, ISaveable, IPredicateEvaluator
    {
        List<QuestStatus> statuses = new List<QuestStatus>();

        //Any change: quest added, objective done, save restored
        public event Action onUpdate;
        //A quest's last objective was just done (hook comic stills, the next quest, etc. here)
        public event Action<Quest> onQuestCompleted;

        //The player's QuestList in this scene (null if there is none)
        public static QuestList Find()
        {
            return FindObjectOfType<QuestList>();
        }

        public void AddQuest(Quest quest)
        {
            if (quest == null || HasQuest(quest)) return;
            QuestStatus newStatus = new QuestStatus(quest);
            statuses.Add(newStatus);
            onUpdate?.Invoke();
        }

        public void CompleteObjective(Quest quest, string objective)
        {
            QuestStatus status = GetQuestStatus(quest);
            if (status == null)
            {
                Debug.LogWarning("CompleteObjective: the player doesn't have the quest " + (quest != null ? quest.name : "(none)"));
                return;
            }
            bool wasComplete = status.IsComplete();
            if (!status.CompleteObjective(objective)) return; //Already done, unknown, or out of order

            if (!wasComplete && status.IsComplete())
            {
                GiveReward(quest);
                onQuestCompleted?.Invoke(quest);
            }
            onUpdate?.Invoke();
        }

        public bool HasQuest(Quest quest)
        {
            return GetQuestStatus(quest) != null;
        }

        public bool IsQuestComplete(Quest quest)
        {
            QuestStatus status = GetQuestStatus(quest);
            return status != null && status.IsComplete();
        }

        public IEnumerable<QuestStatus> GetStatuses()
        {
            return statuses;
        }

        //The newest quest that still has something to do, or null
        public QuestStatus GetCurrentStatus()
        {
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                if (statuses[i].GetQuest() != null && !statuses[i].IsComplete()) return statuses[i];
            }
            return null;
        }

        private QuestStatus GetQuestStatus(Quest quest)
        {
            if (quest == null) return null;
            foreach (QuestStatus status in statuses)
            {
                if (status.GetQuest() == quest)
                {
                    return status;
                }
            }
            return null;
        }

        private void GiveReward(Quest quest)
        {
            var inventory = GetComponent<Inventory>();
            var dropper = GetComponent<ItemDropper>();
            foreach (var reward in quest.GetRewards())
            {
                if (reward.weaponConfig == null) continue;
                bool success = inventory != null && inventory.AddToFirstEmptySlot(reward.weaponConfig, reward.number);
                if (!success && dropper != null)
                {
                    dropper.DropItem(reward.weaponConfig, reward.number);
                }
            }
        }

        public object CaptureState()
        {
            List<object> state = new List<object>();
            foreach (QuestStatus status in statuses)
            {
                if (status.GetQuest() == null) continue;
                state.Add(status.CaptureState());
            }
            return state;
        }

        public void RestoreState(object state)
        {
            List<object> stateList = state as List<object>;
            if (stateList == null) return;

            statuses.Clear();
            foreach (object objectState in stateList)
            {
                var status = new QuestStatus(objectState);
                if (status.GetQuest() != null) statuses.Add(status); //Skip quests whose asset is gone
            }
            onUpdate?.Invoke();
        }

        public bool? Evaluate(string predicate, string[] parameters)
        {
            switch (predicate)
            {
                case "HasQuest":
                    return HasQuest(GetParameterQuest(parameters));
                case "CompletedQuest":
                    return IsQuestComplete(GetParameterQuest(parameters));
                case "CompletedObjective": //parameters: quest name, objective reference
                    var status = GetQuestStatus(GetParameterQuest(parameters));
                    return status != null && parameters.Length > 1 && status.IsObjectiveComplete(parameters[1]);
            }

            return null;
        }

        static Quest GetParameterQuest(string[] parameters)
        {
            return parameters != null && parameters.Length > 0 ? Quest.GetByName(parameters[0]) : null;
        }
    }
}
