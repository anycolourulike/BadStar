using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class DissolveObject : MonoBehaviour
{
    [SerializeField] Material dissolveMaterial; // Material for initial dissolve
    [SerializeField] Material targetMaterial; // Material to blend to

    float dissolveDuration = 7f; // Duration for dissolve animation
    float dissolveStrength;
    
    Material currentMaterial; // Current material being used
    float dissolveTimer = 0f;

    private void Awake()
    {
        currentMaterial = dissolveMaterial;
    }

    public void Start()
    {
        StartDissolver();
    }

    public void StartDissolver()
    {
        StartCoroutine(Dissolver());
    }

    public IEnumerator Dissolver()
    {
        float elapsedTime = 0;

        while (elapsedTime < dissolveDuration)
        {
            Debug.Log("Dissovler");
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(0.8f, 0.2f, elapsedTime / dissolveDuration);
            dissolveMaterial.SetFloat("_Dissolve", dissolveStrength);
            yield return null;
        }
        SetTargetMaterial();
    }

    private void SetTargetMaterial()
    {
        Debug.Log("Set Target Material Called");
        GetComponent<Renderer>().material = targetMaterial;
    }
}