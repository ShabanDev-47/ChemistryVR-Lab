#if UNITY_2019_4_OR_NEWER
using UnityEditor;
using UnityEngine;

namespace com.zibra.liquid
{
    internal static class LiquidEditorMenu
    {

        [MenuItem(ZibraAIPackage.RootMenu + "Info", false, 0)]
        public static void OpenSettings()
        {
            var windowTitle = LiquidSettingsWindow.WindowTitle;
            LiquidSettingsWindow.ShowTowardsInspector(windowTitle.text, windowTitle.image);
        }

        [MenuItem(ZibraAIPackage.RootMenu + "Open Documentation", false, 5)]
        public static void OpenDocumentation()
        {
            string dataPath = Application.dataPath;
            string projectPath = dataPath.Replace("/Assets", "");
            string documentationPath = AssetDatabase.GUIDToAssetPath("09ace81bf2ac0bd4e8c853cda11f7c84");
            Application.OpenURL("file://" + projectPath + "/" + documentationPath);
        }

        [MenuItem(ZibraAIPackage.RootMenu + "Contact us/Discord", false, 1000)]
        public static void OpenDiscord()
        {
#if ZIBRA_LIQUID_FREE_VERSION
            Application.OpenURL("https://discord.gg/Gs6XSrpZbG");
#else
            Application.OpenURL("https://discord.gg/QzypP8n7uB");
#endif
        }

        [MenuItem(ZibraAIPackage.RootMenu + "Contact us/Support E-Mail", false, 1010)]
        public static void OpenSupportEmail()
        {
            Application.OpenURL("mailto:support@zibra.ai");
        }
    }
}
#endif
