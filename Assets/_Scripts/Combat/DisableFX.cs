using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableFX : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine("DisableFXDust");
    }

    IEnumerator DisableFXDust()
    {
        yield return new WaitForSeconds(5);
        Destroy(this.gameObject);
    }
}
