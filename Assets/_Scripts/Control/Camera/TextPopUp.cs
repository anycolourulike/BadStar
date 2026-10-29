using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DentedPixel;
using TMPro;

public class TextPopUp : MonoBehaviour
{
    [SerializeField] float displayDuration;
    [SerializeField] TextMeshProUGUI textPopUP;
    [SerializeField] GameObject textObj;    
    [SerializeField] Camera mainCamera;
    public string message = "";
    public bool isEnabled;
    float timer;
    Vector3 textStartLocalPos;

    void Start()
    {
        timer = 0f;
        //Prefabs can't reference the scene's camera, and the text itself is what drifts
        if (mainCamera == null) mainCamera = Camera.main;
        if (textObj == null && textPopUP != null) textObj = textPopUP.gameObject;
        if (textObj != null) textStartLocalPos = textObj.transform.localPosition;
    }

    void Update()
    {
        if (isEnabled == true)
        {
            timer += Time.deltaTime;
            textPopUP.transform.LookAt(mainCamera.transform);

            //Text stays up for displayDuration, then 2s more before hiding
            if (timer >= displayDuration + 2f)
            {
                ResetTimer();
            }
        }
    }

    void ResetTimer()
    {
        timer = 0f;
        isEnabled = false;
        textPopUP.enabled = false;
    }

    public void EnablePopUp(string message)
    {        
        textPopUP.text = message;
        textPopUP.enabled = true;
        isEnabled = true;
        timer = 0f;

        //Start the drift once from the original position
        LeanTween.cancel(textObj);
        textObj.transform.localPosition = textStartLocalPos;
        LeanTween.move(textObj, textObj.transform.position
            + new Vector3(5f, 0f, 1f), 2f).setEase(LeanTweenType.easeInQuad);
    }

}
