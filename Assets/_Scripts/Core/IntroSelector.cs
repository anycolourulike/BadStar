using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IntroSelector : MonoBehaviour
{

    public GameObject[] canvas;
    public int introNum;


    void Awake()
    {
        foreach(GameObject obj in canvas)
        {
           obj.SetActive(false);
        }
    }
    void Start()
    {
        introNum = LevelManager.Instance.introNum;
        canvas[introNum].SetActive(true);
    }
    
}
