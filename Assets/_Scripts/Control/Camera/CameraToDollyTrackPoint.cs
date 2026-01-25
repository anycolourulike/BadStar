using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraToDollyTrackPoint : MonoBehaviour
{
    [SerializeField] int pathUnit;
    [SerializeField] CameraManager camMan;
    [SerializeField] CinemachineDollyCart dolly;
    [SerializeField] CinemachineVirtualCamera vCam;

    // Start is called before the first frame update
    public void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        {
            if(vCam != null)
            {
                camMan.SwitchPriority(vCam);
                if (dolly != null)
                {
                    dolly.m_Position = pathUnit;
                }
            }
        }
    }
}
