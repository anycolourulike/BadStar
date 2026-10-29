using UnityEditor;

namespace Rambler.EditorTools
{
    //Tools ▸ Test Environment: switches for the editor-only level test setup (see TestEnvironment)
    public static class TestEnvironmentMenu
    {
        const string EnabledMenu = "Tools/Test Environment/Load Managers When Playing A Level";
        const string LoadSaveMenu = "Tools/Test Environment/Load Save File";

        [MenuItem(EnabledMenu)]
        static void ToggleEnabled() => EditorPrefs.SetBool(TestEnvironment.EnabledKey, !TestEnvironment.Enabled);

        [MenuItem(EnabledMenu, true)]
        static bool ToggleEnabledCheck()
        {
            Menu.SetChecked(EnabledMenu, TestEnvironment.Enabled);
            return true;
        }

        [MenuItem(LoadSaveMenu)]
        static void ToggleLoadSave() => EditorPrefs.SetBool(TestEnvironment.LoadSaveKey, !TestEnvironment.LoadSave);

        [MenuItem(LoadSaveMenu, true)]
        static bool ToggleLoadSaveCheck()
        {
            Menu.SetChecked(LoadSaveMenu, TestEnvironment.LoadSave);
            return TestEnvironment.Enabled;
        }
    }
}
