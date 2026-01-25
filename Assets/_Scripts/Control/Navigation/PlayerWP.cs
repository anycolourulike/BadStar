using Rambler.Control;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rambleer.Control
{
    public class PlayerWP : MonoBehaviour
    {
        public string command;
        public GameObject player;
        public AIController playerAICon;
        
        public void OnTriggerEnter(Collider other)
        {
            if(other.gameObject.CompareTag("Player"))
            {
                //player = other.gameObject;
                //playerAICon = other.GetComponent<AIController>();
                //playerAICon.isAtWayPoint = true;
                //playerAICon.command = command;
            }
        }

        //Use Enum / switch statement to control anim at each wp


        public void OnTriggerExit(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                //playerAICon.isAtWayPoint = false;

            }
        }
    }
}
