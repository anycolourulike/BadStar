using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Rambler.Dialogue
{
    public class DialogueTrigger : MonoBehaviour
    {
        [SerializeField] string action;
        [SerializeField] UnityEvent onTrigger;
        [SerializeField] GameObject thisChar;
        [SerializeField] GameObject thisChar2;

        public void Trigger(string actionToTrigger)
        {
            if (actionToTrigger == action)
            {
                onTrigger.Invoke();
            }
        }

        public void ChangeTag()
        {
            thisChar.tag = "Enemy";
            thisChar2.tag = "Enemy";
        }
    }
}
