using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rambler.Attributes;

namespace Rambler.Core
{
    //Runs a level's enemy waves. Every object tagged "SecTeam" is a wave and its children tagged "Enemy"
    //are the wave's enemies. Waves come in Hierarchy order (top first), one at a time. When the last
    //wave is dead the portals switch on and the "Proceed To Next Area" pop-up shows.
    //Leave every wave active in the scene so the save system can restore its enemies; BeginLevel
    //switches the later ones off once the save has loaded.
    public class EnemySpawner : MonoBehaviour
    {
        const string WaveTag = "SecTeam";
        const string PopUpPath = "/PlayerCore/HUD/PopUp";

        [Header("Read only (for debugging)")]
        public int deadEnemyCount;   //Enemies killed in the current wave
        public int nextWaveCount;    //Enemies in the current wave, including ones that joined mid-wave
        public GameObject activeSecTeam;
        public GameObject endLevelTxt;
        public bool levelLoaded;
        public bool levelCleared;

        public List<GameObject> portals = new List<GameObject>();
        public List<GameObject> secTeam = new List<GameObject>();      //Waves still to come, in order
        public List<GameObject> enemiesList = new List<GameObject>();  //Living enemies in the current wave

        bool hadWaves;

        public void FindPopUp()
        {
            if (endLevelTxt != null) return;
            endLevelTxt = GameObject.Find(PopUpPath);
            if (endLevelTxt != null) return;
            //Find skips inactive objects, and the pop-up is saved switched off
            var hud = GameObject.Find("/PlayerCore/HUD");
            var popUp = hud != null ? hud.transform.Find("PopUp") : null;
            if (popUp != null) endLevelTxt = popUp.gameObject;
            else Debug.LogWarning("[EnemySpawner] No PopUp under PlayerCore/HUD; the level-cleared text won't show");
        }

        //Called by LevelManager on every level start, after the save has loaded
        public void BeginLevel()
        {
            FindPopUp();
            if (endLevelTxt != null) endLevelTxt.SetActive(false);

            deadEnemyCount = 0;
            nextWaveCount = 0;
            enemiesList.Clear();
            activeSecTeam = null;
            levelCleared = false;

            secTeam = FindWaves();
            hadWaves = secTeam.Count > 0;
            //Later waves off straight away, so they can't fight or be seen before their turn
            for (int i = 1; i < secTeam.Count; i++) secTeam[i].SetActive(false);

            StartNextWave();
            levelLoaded = true;

            if (activeSecTeam != null)
            {
                HidePortals();
            }
            else
            {
                //Nothing to fight: no waves, or they were all beaten before this save. Open up quietly.
                levelCleared = true;
                ShowPortals();
            }
        }

        void Update()
        {
            if (!levelLoaded || levelCleared) return;

            //Enemies destroyed some other way (or unloaded with their area) no longer count
            enemiesList.RemoveAll(e => e == null);
            if (enemiesList.Count > 0) return;

            EndActiveWave();
            StartNextWave();
            if (activeSecTeam == null) LevelCleared();
        }

        void LevelCleared()
        {
            levelCleared = true;
            ShowPortals();
            if (hadWaves && endLevelTxt != null) endLevelTxt.SetActive(true);
        }

        //Makes the next wave that still has living enemies the active one. Leaves activeSecTeam null if none is left.
        void StartNextWave()
        {
            //Waves in areas that streamed in after the level started join the queue in Hierarchy order
            foreach (var wave in FindWaves())
            {
                if (secTeam.Contains(wave)) continue;
                secTeam.Add(wave);
                wave.SetActive(false);
            }
            secTeam.RemoveAll(w => w == null);
            secTeam.Sort(CompareHierarchyOrder);

            while (secTeam.Count > 0)
            {
                var wave = secTeam[0];
                wave.SetActive(true);
                var enemies = LivingEnemiesIn(wave);
                if (enemies.Count == 0)
                {
                    //Beaten before this save, or set up with no enemies
                    secTeam.RemoveAt(0);
                    Destroy(wave);
                    continue;
                }

                activeSecTeam = wave;
                enemiesList.AddRange(enemies);
                deadEnemyCount = 0;
                nextWaveCount = enemiesList.Count;
                return;
            }
            activeSecTeam = null;
        }

        void EndActiveWave()
        {
            if (activeSecTeam == null) return;
            secTeam.Remove(activeSecTeam);
            Destroy(activeSecTeam);
            activeSecTeam = null;
        }

        //Adds an enemy that arrived mid-level (e.g. deployed by the ship) to the current wave,
        //so the wave and level don't end while it is still alive
        public void RegisterEnemy(GameObject enemy)
        {
            if (enemy == null || enemiesList.Contains(enemy)) return;
            enemiesList.Add(enemy);
            nextWaveCount++;
        }

        //An enemy died or left the enemy side (hacked). Unparented so its body survives the wave being cleared up.
        public void RemoveEnemy(GameObject enemyToRemove)
        {
            if (enemyToRemove == null || !enemiesList.Remove(enemyToRemove)) return;
            enemyToRemove.transform.SetParent(null);
            deadEnemyCount++;
        }

        public void HidePortals()
        {
            //Add rather than replace: hidden portals can't be found by tag again
            foreach (var portal in GameObject.FindGameObjectsWithTag("Portal"))
            {
                if (!portals.Contains(portal)) portals.Add(portal);
                portal.SetActive(false);
            }
        }

        public void ShowPortals()
        {
            portals.RemoveAll(p => p == null);
            foreach (var portal in portals) portal.SetActive(true);
        }

        static List<GameObject> LivingEnemiesIn(GameObject wave)
        {
            var found = new List<GameObject>();
            foreach (var t in wave.GetComponentsInChildren<Transform>())
            {
                if (!t.CompareTag("Enemy")) continue;
                var health = t.GetComponent<Health>();
                if (health != null && health.IsDead()) continue;
                found.Add(t.gameObject);
            }
            return found;
        }

        //Every SecTeam in the loaded scenes, switched on or off, top of the Hierarchy first
        static List<GameObject> FindWaves()
        {
            var waves = new List<GameObject>();
            foreach (var t in FindObjectsOfType<Transform>(true))
            {
                if (t.CompareTag(WaveTag) && t.gameObject.scene.isLoaded) waves.Add(t.gameObject);
            }
            waves.Sort(CompareHierarchyOrder);
            return waves;
        }

        static int CompareHierarchyOrder(GameObject a, GameObject b)
        {
            int sa = SceneOrder(a.scene), sb = SceneOrder(b.scene);
            if (sa != sb) return sa.CompareTo(sb);
            var pa = SiblingPath(a.transform);
            var pb = SiblingPath(b.transform);
            for (int i = 0; i < Mathf.Min(pa.Count, pb.Count); i++)
            {
                if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
            }
            return pa.Count.CompareTo(pb.Count);
        }

        static int SceneOrder(Scene scene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i) == scene) return i;
            }
            return int.MaxValue;
        }

        static List<int> SiblingPath(Transform t)
        {
            var path = new List<int>();
            for (; t != null; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            return path;
        }
    }
}
