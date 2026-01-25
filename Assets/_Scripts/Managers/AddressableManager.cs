using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System;
using UnityEngine;

public class AddressableManager : MonoBehaviour
{
    bool clearPreviousScene;

    //SceneInstance previousLoadedScene;

    public void LoadAddressableLevel(string addressableKey)
    {
        if(clearPreviousScene)
        {
            // Addressables.UnloadAsyncScene(previousLoadedScene).Completed += (asyncHandle) =>
            // {
            //     clearPreviousScene = true;
            //     previousLoadedScene = new SceneInstance()
            // }
        }
    }

}
