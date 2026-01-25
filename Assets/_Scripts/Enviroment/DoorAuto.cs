using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorAuto : MonoBehaviour
{

    public GameObject player;
    float doorTimer = 7f;
    public bool doorIsOpen;
    Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {

        if (doorIsOpen == true)
        {
            doorTimer -= Time.deltaTime;
        }

        if (doorTimer <= 0f)
        {
            anim.SetTrigger("Close");
            doorTimer = 10f;
            doorIsOpen = false;
        }

    }

    void OnTriggerEnter(Collider other)
    {
        if ((other.gameObject.tag == "Enemy") || (other.gameObject == player))
        {
            anim.SetTrigger("Open");
            doorIsOpen = true;            
        }
    }
}
