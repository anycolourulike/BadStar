using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;

namespace Rambler.Combat
{
    public class AggroGroup : MonoBehaviour
    {
        [SerializeField] Fighter[] fighters;
        [SerializeField] bool activateOnStart = false;

        private void Start() 
        {
            Activate(shouldActivate: activateOnStart);
        }

        public void Activate(bool shouldActivate)
        {
            foreach (Fighter fighter in fighters)
            {
                FieldOfView target = fighter.GetComponent<FieldOfView>();
                if (target != null)
                {
                    target.enabled = shouldActivate;
                }
                fighter.enabled = shouldActivate;
            }
        }
    }
}