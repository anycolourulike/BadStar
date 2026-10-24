using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AsyncLoad : MonoBehaviour
{
    public string prefabAddress; // Address of the prefab if using Addressables
    public GameObject prefab;    // Prefab reference if using direct loading

    AsyncOperationHandle<GameObject> handle;

    private void Start()
    {
        // Start loading the prefab asynchronously
        StartCoroutine(LoadPrefabAsync());
    }

    private void OnDestroy()
    {
        // Release the Addressables asset so its memory can be freed
        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    private IEnumerator LoadPrefabAsync()
    {
        // If using Addressables (recommended for large assets)
        if (!string.IsNullOrEmpty(prefabAddress))
        {
            handle = Addressables.LoadAssetAsync<GameObject>(prefabAddress);
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
            // The prefab is already referenced, so it loaded with the scene;
            // reloading it by name from Resources fails unless it lives in a Resources folder
            OnPrefabLoaded(prefab);
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
