using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Rambler.Control;

namespace Rambler.SceneManagement
{
    //Streams a level's area scenes in and out around the player. The level scene itself stays loaded as the core
    //(player, HUD, cameras, sky, NavMesh, lift, train); each area is an additive Addressables scene that loads when the
    //player or the camera's focus comes within loadDistance of the area's bounds and unloads past unloadDistance.
    //Areas that hold gameplay state (doors, terminals, enemies) can be marked to stay loaded once they have loaded.
    public class AreaStreamer : MonoBehaviour
    {
        [Serializable]
        public class Area
        {
            public string name;
            public AssetReference scene;
            [Tooltip("Scene name, used to spot an area that is already open (e.g. edited alongside the level in the editor)")]
            public string sceneName;
            [Tooltip("World bounds of everything in the area")] public Bounds bounds;
            [Tooltip("Never unloads once loaded, so its state (doors, terminals, enemies) is kept")] public bool keepOnceLoaded;
            [Tooltip("Optional (0 = the streamer's default). Shorter for closed spaces that cannot be seen from outside, e.g. an underground cavern")]
            public float loadDistance;
            [NonSerialized] public AsyncOperationHandle<SceneInstance> handle;
            [NonSerialized] public bool loading, loaded, unloading, external;
        }

        [SerializeField] Area[] areas;
        [Tooltip("Load an area when the player or camera focus is this close to its bounds (m). The fog hides everything past ~96 m.")]
        [SerializeField] float loadDistance = 110f;
        [Tooltip("Unload it again past this distance (m); the gap stops areas flickering in and out at the edge")]
        [SerializeField] float unloadDistance = 150f;
        [SerializeField] float checkInterval = 0.5f;

        public static AreaStreamer Instance { get; private set; }
        //Levels without a streamer are ready straight away
        public static bool InitialAreasReady => Instance == null || Instance.initialDone;

        bool initialDone;
        Transform player;
        float nextCheck;

        void Awake()
        {
            Instance = this;
        }

        void OnEnable() => PlayerSelector.OnPlayerObjectChanged += OnPlayerChanged;

        void OnDisable()
        {
            PlayerSelector.OnPlayerObjectChanged -= OnPlayerChanged;
            if (Instance == this) Instance = null;
        }

        void OnPlayerChanged(GameObject newPlayer) => player = newPlayer != null ? newPlayer.transform : null;

        IEnumerator Start()
        {
            //Areas already open (multi-scene editing in the editor) count as loaded
            foreach (var a in areas)
            {
                var s = SceneManager.GetSceneByName(a.sceneName);
                if (s.IsValid() && s.isLoaded) { a.loaded = true; a.external = true; }
            }
            yield return null; //PlayerSelector picks the active player in its own Start
            FindPlayer();
            Check();
            while (AnyLoading()) yield return null;
            initialDone = true;
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;
            Check();
        }

        void FindPlayer()
        {
            if (player != null) return;
            var selector = FindFirstObjectByType<PlayerSelector>();
            if (selector != null && selector.activePlayer != null) player = selector.activePlayer.transform;
        }

        bool AnyLoading()
        {
            foreach (var a in areas) if (a.loading) return true;
            return false;
        }

        //Nearest of the player and the camera focus (the camera joystick can pan the view away from the player)
        float DistanceTo(Area a)
        {
            float d = float.MaxValue;
            if (player != null) d = Mathf.Sqrt(a.bounds.SqrDistance(player.position));
            if (CameraManager.Instance != null) d = Mathf.Min(d, Mathf.Sqrt(a.bounds.SqrDistance(CameraManager.Instance.FocusPoint)));
            return d;
        }

        void Check()
        {
            FindPlayer();
            if (player == null && CameraManager.Instance == null) return;
            foreach (var a in areas)
            {
                if (a.loading || a.unloading) continue;
                float d = DistanceTo(a);
                float load = a.loadDistance > 0f ? a.loadDistance : loadDistance;
                float unload = load + (unloadDistance - loadDistance);
                if (!a.loaded && d <= load) Load(a);
                else if (a.loaded && !a.keepOnceLoaded && d > unload) Unload(a);
            }
        }

        void Load(Area a)
        {
            a.loading = true;
            a.handle = Addressables.LoadSceneAsync(a.scene, LoadSceneMode.Additive);
            a.handle.Completed += h =>
            {
                a.loading = false;
                a.loaded = h.Status == AsyncOperationStatus.Succeeded;
                if (!a.loaded) Debug.LogWarning("AreaStreamer: could not load area " + a.name);
            };
        }

        void Unload(Area a)
        {
            a.unloading = true;
            if (a.external)
            {
                var op = SceneManager.UnloadSceneAsync(a.sceneName);
                if (op == null) { a.unloading = false; return; }
                op.completed += _ => { a.unloading = false; a.loaded = false; a.external = false; };
                return;
            }
            Addressables.UnloadSceneAsync(a.handle).Completed += _ => { a.unloading = false; a.loaded = false; };
        }

        void OnDrawGizmosSelected()
        {
            if (areas == null) return;
            foreach (var a in areas)
            {
                Gizmos.color = a.keepOnceLoaded ? new Color(1f, 0.6f, 0.2f) : new Color(0.3f, 0.8f, 1f);
                Gizmos.DrawWireCube(a.bounds.center, a.bounds.size);
            }
        }
    }
}
