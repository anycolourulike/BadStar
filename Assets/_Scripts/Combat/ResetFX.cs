using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetFX : MonoBehaviour
{
    [SerializeField] GameObject jumpFX;

    // Start is called before the first frame update
    void OnEnable()
    {
        StartCoroutine("ResetJumpFX");
    }

   IEnumerator ResetJumpFX()
    {
        yield return new WaitForSeconds(7);
        jumpFX.SetActive(false);
    }
}
