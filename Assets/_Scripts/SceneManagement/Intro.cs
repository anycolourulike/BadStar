using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class Intro : MonoBehaviour
{
    [SerializeField] Animator anim;
    [SerializeField] Animator anim1;
    [SerializeField] Animator anim2;
    [SerializeField] GameObject Img;
    [SerializeField] Button next;
    
   
    LevelManager levelManager;
    public GameObject[] Imgs;
   // GameObject nextImg;
    GameObject curImg;    
    public Fader fader;
    public int sceneRef = 2; //Loads RobotLab

   
    void Start()
    {
        fader = FindObjectOfType<Fader>();
        fader.FadeToBlackImmediate();
        levelManager = FindObjectOfType<LevelManager>(); 
        fader.FadeToClear(2);
        curImg = Img;
        Img.SetActive(true);
        StartCoroutine("PlayImg");
    }        

    IEnumerator PlayImg()
    {
        anim.SetTrigger("Img"); 
        yield return new WaitForSeconds(2f);
        anim1.SetTrigger("Img");
        yield return new WaitForSeconds(0.7f);
        anim2.SetTrigger("Img");
        yield return new WaitForSeconds(1f);
        next.gameObject.SetActive(true);
    }

    public void PlayNextScene()
    {
        if (curImg != null)
        {
            curImg.SetActive(false);
        }
        fader.FadeToClear(1);
        next.gameObject.SetActive(false);
        sceneRef = levelManager.introSubSceneRef;
        levelManager.sceneRef = sceneRef;
        levelManager.StartCoroutine("LoadLoading");
    }

    //public void PlayNextImg()
    //{
    //    curImg.SetActive(false);
    //    NextImg();

    //    if ( curImg == null )
    //    {
    //        PlayNextScene();
    //    }
    //    else
    //    {
    //       curImg.gameObject.SetActive(true); 
    //    }
    //}

    //public void PlayPrevImg()
    //{
    //    curImg.SetActive(false);
    //    PrevImg();

    //    if ( curImg != null )
    //    { 
    //        curImg.gameObject.SetActive(true); 
    //    }
    //}



    //void NextImg()
    //{   
    //    if(counter == 7) 
    //    {
    //        PlayNextScene();
    //        curImg = null;
    //        return;
    //    }             
    //    nextImg = Imgs[counter + 1];
    //    counter ++;
    //    curImg = nextImg;
    //}

    //void PrevImg()
    //{    
    //    if(counter == 0)
    //    {
    //       counter = 0;
    //       return;
    //    }   
    //    else
    //    { 
    //       nextImg = Imgs[counter - 1];
    //       counter -- ;
    //       curImg = nextImg;
    //    }   
    //}    
}
