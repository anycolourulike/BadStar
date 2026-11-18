using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Quests
{
    //Progress on one quest at runtime: which objectives are done.
    public class QuestStatus
    {
        Quest quest;
        List<string> completedObjectives = new List<string>();

        [System.Serializable]
        class QuestStatusRecord
        {
            public string questName;
            public List<string> completedObjectives;
        }

        public QuestStatus(Quest quest)
        {
            this.quest = quest;
        }

        //Rebuilds a status from a save. GetQuest() is null if the quest asset can no longer be found.
        public QuestStatus(object objectState)
        {
            QuestStatusRecord state = objectState as QuestStatusRecord;
            if (state == null) return;
            quest = Quest.GetByName(state.questName);
            if (state.completedObjectives != null) completedObjectives = new List<string>(state.completedObjectives);
        }

        public Quest GetQuest()
        {
            return quest;
        }

        public bool IsComplete()
        {
            if (quest == null) return false;
            foreach (var objective in quest.GetObjectives())
            {
                if (!completedObjectives.Contains(objective.reference))
                {
                    return false;
                }
            }
            return true;
        }

        public int GetCompletedCount()
        {
            return completedObjectives.Count;
        }

        public bool IsObjectiveComplete(string objective)
        {
            return completedObjectives.Contains(objective);
        }

        //The first objective (top to bottom) not done yet, or null when the quest is complete
        public Quest.Objective GetCurrentObjective()
        {
            if (quest == null) return null;
            foreach (var objective in quest.GetObjectives())
            {
                if (!completedObjectives.Contains(objective.reference)) return objective;
            }
            return null;
        }

        //Returns true if this changed anything
        public bool CompleteObjective(string objective)
        {
            if (quest == null || !quest.HasObjective(objective)) return false;
            if (completedObjectives.Contains(objective)) return false;
            if (quest.IsInOrder())
            {
                var current = GetCurrentObjective();
                if (current == null || current.reference != objective) return false;
            }
            completedObjectives.Add(objective);
            return true;
        }

        public object CaptureState()
        {
            QuestStatusRecord state = new QuestStatusRecord();
            state.questName = quest.name;
            state.completedObjectives = new List<string>(completedObjectives); //A copy, so later progress can't change a taken save
            return state;
        }
    }
}
