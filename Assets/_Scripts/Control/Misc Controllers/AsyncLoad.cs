using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AsyncLoad : MonoBehaviour
{
    public string prefabAddress; // Address of the prefab if using Addressables
    public GameObject prefab;    // Prefab reference if using direct loading

    private void Start()
    {
        // Start loading the prefab asynchronously
        StartCoroutine(LoadPrefabAsync());
    }

    private IEnumerator LoadPrefabAsync()
    {
        // If using Addressables (recommended for large assets)
        if (!string.IsNullOrEmpty(prefabAddress))
        {
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(prefabAddress);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                OnPrefabLoaded(handle.Result);
            }
            else
            {
                Debug.LogError("Failed to load prefab from address: " + prefabAddress);
            }
        }
        else if (prefab != null) // If using direct loading
        {
            ResourceRequest resourceRequest = Resources.LoadAsync<GameObject>(prefab.name);
            yield return resourceRequest;

            if (resourceRequest.asset != null)
            {
                OnPrefabLoaded(resourceRequest.asset as GameObject);
            }
            else
            {
                Debug.LogError("Failed to load prefab: " + prefab.name);
            }
        }
        else
        {
            Debug.LogError("No prefab reference or address provided!");
        }
    }

    private void OnPrefabLoaded(GameObject loadedPrefab)
    {
        Debug.Log("Prefab loaded successfully: " + loadedPrefab.name);

        // Instantiate the prefab in the scene
        GameObject instantiatedPrefab = Instantiate(loadedPrefab, Vector3.zero, Quaternion.identity);

        // Additional logic after prefab is loaded
        // For example: Start an animation, set properties, etc.
    }
}
