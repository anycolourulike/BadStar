using UnityEngine;

namespace Rambler.Quests
{
    //Call GiveQuest from a UnityEvent (a DialogueTrigger action, a button, an ObjectiveTrigger's onTriggered).
    public class QuestGiver : MonoBehaviour
    {
        [SerializeField] Quest quest;

        public void GiveQuest()
        {
            QuestList questList = QuestList.Find();
            if (questList == null)
            {
                Debug.LogWarning("QuestGiver on " + name + ": no QuestList in the scene");
                return;
            }
            questList.AddQuest(quest);
        }
    }
}
