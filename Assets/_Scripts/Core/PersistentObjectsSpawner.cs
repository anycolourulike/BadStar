using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Rambler.Core
{
    public class PersistentObjectsSpawner : MonoBehaviour
    {
        [SerializeField] GameObject persistentObjecetPrefab;

        static bool hasSpawned = false;

        private void Awake()
        {
            if (hasSpawned) return;

            SpawnPersistentObjects();

            hasSpawned = true;
        }

        private void SpawnPersistentObjects()
        {
            Debug.Log("PersistentObjsSpawned");
            GameObject persistentObject = Instantiate(persistentObjecetPrefab);
            DontDestroyOnLoad(persistentObject);
        }
    }
}
