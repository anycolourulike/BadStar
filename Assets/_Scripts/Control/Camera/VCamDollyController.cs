using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Rambler.SceneManagement;

public class VCamDollyController : MonoBehaviour
{
    public CinemachineVirtualCamera vCam;   
    public CinemachineDollyCart dolly;
    public CinemachinePathBase path;
    public float lerpDuration = 0.8f; 
    private float lerpTime = 0f;
    public bool isLerping;
    public int pathUnit;
    public float fov;


    void Update()
    {
        if(isLerping)
        {
            LerpCam();
        }
    }


    public void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        {
            if (dolly != null && path != null)
            {
                isLerping = true;       
            }
        }
    }  

    void LerpCam()
    {
        float initialPathPosition = dolly.m_Position; // Get the current position on the path
        lerpTime += Time.deltaTime;

        // Lerp the path position over time
        float t = Mathf.SmoothStep(0f, 1f, lerpTime / lerpDuration);
        dolly.m_Position = Mathf.Lerp(initialPathPosition, pathUnit, t);

        if (lerpTime >= lerpDuration)
        {
            // Reset lerpTime and mark as not lerping
            lerpTime = 0f;
            isLerping = false;
        }

        SetVirtualCameraFieldOfView(fov);
    }

    void SetVirtualCameraFieldOfView(float targetFOV)
    {
        // Access the lens settings of the virtual camera
        vCam.m_Lens.FieldOfView = targetFOV;
    }       
}
