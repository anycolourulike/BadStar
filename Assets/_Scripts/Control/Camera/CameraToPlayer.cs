using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Rambler.Movement;
using Rambler.SceneManagement;

namespace Rambler.Control
{
    public class CameraToPlayer : MonoBehaviour
    {
        [SerializeField] CameraManager camMan;
        [SerializeField] CinemachineVirtualCamera vCam;

        public void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                if (vCam != null)
                {
                    camMan.SwitchPriority(vCam);
                    vCam.LookAt = other.transform;             
                }
            };
        }
    }
}
