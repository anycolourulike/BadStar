using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DentedPixel;
using Rambler.Control;

public class PortalLerp : MonoBehaviour
{
    [SerializeField] GameObject portal;
    [SerializeField] AIController aICont;

    private void Start()
    {
        StartCoroutine(ScalePortal());
    }

    IEnumerator ScalePortal()
    {
        LeanTween.scale(portal, new Vector3 (1, 1, 0), 3f);
        yield return new WaitForSeconds(4f);
        aICont.IsPatrolling();
        portal.SetActive(false);
    }
}
