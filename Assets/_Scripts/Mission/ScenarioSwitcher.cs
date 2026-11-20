using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rambler.Mission
{
    //Picks which Sandbox setup is live: each option is a root object (a mini scenario, the waves, the heist). Only the
    //chosen one is switched on. Buttons on screen restart the scene into another one (remembered between plays).
    [DefaultExecutionOrder(-500)] //Before the setups' own Awake/Start
    public class ScenarioSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public struct Option { public string label; public GameObject root; }

        [SerializeField] Option[] options;
        [Tooltip("Used the first time (nothing remembered yet).")]
        [SerializeField] int defaultIndex;
        [SerializeField] bool showButtons = true;

        const string Key = "Rambler.Sandbox.Scenario";
        int current;
        bool menuOpen;

        public static int Pending = -1; //Set before a reload to choose without the menu

        void Awake()
        {
            current = Pending >= 0 ? Pending : PlayerPrefs.GetInt(Key, defaultIndex);
            Pending = -1;
            if (current < 0 || current >= options.Length) current = Mathf.Clamp(defaultIndex, 0, options.Length - 1);
            for (int i = 0; i < options.Length; i++) if (options[i].root != null) options[i].root.SetActive(i == current);
            Debug.Log("[Sandbox] scenario: " + options[current].label);
        }

        public void Choose(int i)
        {
            PlayerPrefs.SetInt(Key, i);
            PlayerPrefs.Save();
            Pending = i;
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            //Test scenes like Sandbox aren't in the build list; the editor can still reload them by path
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }

        GUIStyle style;
        void OnGUI()
        {
            if (!showButtons || options == null || options.Length == 0) return;
            if (style == null) style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * 0.024f) };
            float w = Screen.width * 0.22f, h = Screen.height * 0.05f, x = Screen.width - w - Screen.width * 0.01f, y = Screen.height * 0.12f;
            if (GUI.Button(new Rect(x, y, w, h), (menuOpen ? "v " : "> ") + options[current].label, style)) menuOpen = !menuOpen;
            if (!menuOpen) return;
            for (int i = 0; i < options.Length; i++)
                if (i != current && GUI.Button(new Rect(x, y + h * (i + 1.1f), w, h), options[i].label, style)) Choose(i);
        }
    }
}
