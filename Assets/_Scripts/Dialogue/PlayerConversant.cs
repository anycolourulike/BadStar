using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rambler.Utils;

namespace Rambler.Dialogue
{
    public class PlayerConversant : MonoBehaviour
    {
        [SerializeField] string playerName;
        Dialogue currentDialogue;
        DialogueNode currentNode = null;
        AIConversant currentConversant = null;
        bool isChoosing = false;

        public event Action onConversationUpdated;

        public void StartDialogue(AIConversant newConversant, Dialogue newDialogue)
        {
            currentConversant = newConversant;
            currentDialogue = newDialogue;
            currentNode = currentDialogue.GetRootNode();
            TriggerEnterAction();
            onConversationUpdated?.Invoke();
        }

        public void Quit()
        {
            currentDialogue = null;
            TriggerExitAction();
            currentNode = null;
            isChoosing = false;
            currentConversant = null;
            onConversationUpdated?.Invoke();
        }

        public bool IsActive()
        {
            return currentDialogue != null;
        }

        public bool IsChoosing()
        {
            return isChoosing;
        }

        public string GetText()
        {
            if (currentNode == null)
            {
                return "";
            }
            return currentNode.GetText();
        }

        public string GetCurrentConversantName()
        {
            //Also covers a final player line shown on its own (see SelectChoice)
            if (isChoosing || (currentNode != null && currentNode.IsPlayerSpeaking()))
            {
                return playerName;
            }
            return currentConversant != null ? currentConversant.GetName() : "";
        }

        public IEnumerable<DialogueNode> GetChoices()
        {
            return FilterOnCondition(currentDialogue.GetPlayerChildren(currentNode));
        }

        public void SelectChoice(DialogueNode chosenNode)
        {
            currentNode = chosenNode;
            TriggerEnterAction();
            isChoosing = false;
            if (!HasNext())
            {
                //Last line of the conversation: show the chosen line with Next hidden, so the player leaves with
                //the Quit button (which is also wired to resume the game and bring the HUD back)
                onConversationUpdated?.Invoke();
                return;
            }
            Next();
        }
    
        public void Next()
        {
            int numPlayerResponses = FilterOnCondition
            (currentDialogue.GetPlayerChildren(currentNode)).Count();
            
            if (numPlayerResponses > 0)
            {
                isChoosing = true;
                TriggerExitAction();
                onConversationUpdated?.Invoke();
                return;
            }

            DialogueNode[] children = FilterOnCondition(currentDialogue.GetAIChildren(currentNode)).ToArray();
            if (children.Length == 0)
            {
                //Nothing left to say: stay on this line (Next is hidden, Quit ends it)
                onConversationUpdated?.Invoke();
                return;
            }
            int randomIndex = UnityEngine.Random.Range(0, children.Length);
            TriggerExitAction();
            currentNode = children[randomIndex];
            TriggerEnterAction();
            onConversationUpdated?.Invoke();
        }

        public bool HasNext()
        {
            return FilterOnCondition(currentDialogue.GetAllChildren(currentNode)).Count() > 0;
        }

        private IEnumerable<DialogueNode> FilterOnCondition(IEnumerable<DialogueNode> inputNode)
        {
            foreach (var node in inputNode)
            {
                if (node.CheckCondition(GetEvaluators()))
                {
                    yield return node;
                }
            }
        }

        private IEnumerable<IPredicateEvaluator> GetEvaluators()
        {
            return GetComponents<IPredicateEvaluator>();
        }

        private void TriggerEnterAction()
        {
            if (currentNode != null)
            {
                TriggerAction(currentNode.GetOnEnterAction());
            }
        }

        private void TriggerExitAction()
        {
            if (currentNode != null)
            {
                TriggerAction(currentNode.GetOnExitAction());
            }
        }

        private void TriggerAction(string action)
        {
            if (string.IsNullOrEmpty(action) || currentConversant == null) return;

            foreach (DialogueTrigger trigger in currentConversant.GetComponents<DialogueTrigger>())
            {
                trigger.Trigger(action);
            }
        }
    }
}
