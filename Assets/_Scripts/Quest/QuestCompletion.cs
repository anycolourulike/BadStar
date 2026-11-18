using UnityEngine;

namespace Rambler.Quests
{
    //Call CompleteObjective from a UnityEvent (a DialogueTrigger action, a pickup, a death event).
    public class QuestCompletion : MonoBehaviour
    {
        [SerializeField] Quest quest;
        [Tooltip("The objective's reference in the quest asset")]
        [SerializeField] string objective;

        public void CompleteObjective()
        {
            QuestList questList = QuestList.Find();
            if (questList == null)
            {
                Debug.LogWarning("QuestCompletion on " + name + ": no QuestList in the scene");
                return;
            }
            questList.CompleteObjective(quest, objective);
        }
    }
}
