using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Rambler.Movement;
using Rambler.SceneManagement;

namespace Rambler.Control
{
    public class WalkThroughDoor : MonoBehaviour
    {  
        [SerializeField] Transform tran;
        [SerializeField] Collider otherCol;

        public void OnTriggerEnter(Collider other)
        {            
            if(other.gameObject.CompareTag("Player"))
            {
                 DisableCol();
                 var mover = other.GetComponent<Mover>();
                 mover.MoveTo(tran.transform.position, 7f);                                           
            };
        }

        void DisableCol()
        {
            otherCol.enabled = false;
            Invoke("EnableCol", 5f);
        }

        void EnableCol()
        {
            otherCol.enabled = true;
        }
    }
}
