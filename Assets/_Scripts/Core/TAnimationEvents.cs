using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;
using Rambler.Attributes;

namespace Rambler.Combat
{
    public class TAnimationEvents : MonoBehaviour
    {
        [SerializeField] TimbertoesCon aICon;
        [SerializeField] TFighter fighter;
        
        public void ReloadFinished()
        {
            Debug.Log("ReloadFinished");
            fighter.ReloadFin();
            aICon.ReloadingFalse();
        }
    }
}
