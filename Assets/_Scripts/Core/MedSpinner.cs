using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MedSpinner : MonoBehaviour
{
    GameObject obj;

    void Start()
    {
        obj = this.gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        obj.transform.Rotate(new Vector3(0, 90, 0) * Time.deltaTime);
    }
}
