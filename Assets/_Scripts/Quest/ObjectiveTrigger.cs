using UnityEngine;
using UnityEngine.Events;
using Rambler.Control;

namespace Rambler.Quests
{
    //A trigger volume that completes a quest objective when the player walks in.
    //Put it on an object with a Collider set to Is Trigger. It also fires onTriggered, e.g. to give the next
    //quest, start a dialogue or show a comic sequence.
    [RequireComponent(typeof(Collider))]
    public class ObjectiveTrigger : MonoBehaviour
    {
        [SerializeField] Quest quest;
        [Tooltip("The objective's reference in the quest asset")]
        [SerializeField] string objective;
        [Tooltip("Give the player the quest first if they don't have it yet")]
        [SerializeField] bool giveQuestIfMissing = false;
        [Tooltip("Only react while this objective is the one on screen (walking through early does nothing)")]
        [SerializeField] bool onlyWhenCurrent = true;
        [Tooltip("Switch the trigger off after it has fired once")]
        [SerializeField] bool once = true;
        [SerializeField] UnityEvent onTriggered;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            //Any playable character counts (Rambler, or whoever the player has switched to)
            if (other.GetComponentInParent<PlayerController>() == null && !other.CompareTag("Player")) return;

            QuestList questList = QuestList.Find();
            if (questList == null) return;

            if (quest != null)
            {
                if (!questList.HasQuest(quest))
                {
                    if (!giveQuestIfMissing) return;
                    questList.AddQuest(quest);
                }

                if (onlyWhenCurrent)
                {
                    var current = questList.GetCurrentStatus();
                    var currentObjective = current != null ? current.GetCurrentObjective() : null;
                    if (current == null || current.GetQuest() != quest || currentObjective == null ||
                        currentObjective.reference != objective) return;
                }

                questList.CompleteObjective(quest, objective);
            }

            onTriggered?.Invoke();
            if (once) gameObject.SetActive(false);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
