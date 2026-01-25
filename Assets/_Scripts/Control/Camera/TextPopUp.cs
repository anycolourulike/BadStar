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
    

    void Start()
    {  
        timer = 0f;
    }

    void Update()
    {
        if (isEnabled == true)
        {
            timer += Time.deltaTime;
            textPopUP.enabled = true;            
            textPopUP.transform.LookAt(mainCamera.transform);
            LeanTween.move(textObj, textPopUP.transform.position 
                + new Vector3(5f, 0f, 1f), 2f).setEase(LeanTweenType.easeInQuad);

            if (timer >= displayDuration)
            {                
                Invoke(nameof(ResetTimer), 2f);
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
    }

}
