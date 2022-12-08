using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace com.zibra.liquid.Editor
{
    internal class ZibraWelcomeWindow : EditorWindow
    {
        public static GUIContent WindowTitle => new GUIContent("Zibra Liquids Welcome Screen");

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            string PrefsKey = "ZibraLiquidsWelcomeScreenSeenV1";

#if ZIBRA_LIQUID_FREE_VERSION
            PrefsKey += "Free";
#endif

            if (!EditorPrefs.GetBool(PrefsKey))
            {
                ShowWindow();
                EditorPrefs.SetBool(PrefsKey, true);
            }
        }

        [MenuItem("Zibra AI/Zibra AI - Liquids/Open Welcome Screen", false, 8)]
        private static void ShowWindow()
        {
            ZibraWelcomeWindow window = (ZibraWelcomeWindow)EditorWindow.GetWindow(typeof(ZibraWelcomeWindow));
            window.titleContent = WindowTitle;
            window.Show();
        }

        private void OnEnable()
        {
            // Reference to the root of the window.
            var root = rootVisualElement;

#if ZIBRA_LIQUID_FREE_VERSION
            minSize = maxSize = new Vector2(480, 530);
#else
            minSize = maxSize = new Vector2(480, 505);
#endif

#if ZIBRA_LIQUID_FREE_VERSION
            var versionSpecificUSSAssetPath = AssetDatabase.GUIDToAssetPath("aec7970fdff804e428d1ef07405b5c80");
#else
            var versionSpecificUSSAssetPath = AssetDatabase.GUIDToAssetPath("1facc61fe8c80a74881451154e98d65c");
#endif
            var versionSpecificStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(versionSpecificUSSAssetPath);
            root.styleSheets.Add(versionSpecificStyleSheet);

            var uxmlAssetPath = AssetDatabase.GUIDToAssetPath("4a3534c7503328247ab95f240be69432");
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlAssetPath);
            visualTree.CloneTree(root);

            root.Q<Button>("Discord").clicked += DiscordClick;
            root.Q<Button>("E-Mail").clicked += EmailClick;
            root.Q<Button>("Documentation").clicked += DocumentationClick;
            root.Q<Button>("Tutorials").clicked += TutorialsClick;
            root.Q<Button>("TutorialScenes").clicked += TutorialScenesClick;
            root.Q<Button>("FullVersion").clicked += FullVersionClick;
        }

        private void DiscordClick()
        {
            EditorApplication.ExecuteMenuItem("Zibra AI/Zibra AI - Liquids/Contact us/Discord");
        }

        private void EmailClick()
        {
            EditorApplication.ExecuteMenuItem("Zibra AI/Zibra AI - Liquids/Contact us/Support E-Mail");
        }

        private void DocumentationClick()
        {
            EditorApplication.ExecuteMenuItem("Zibra AI/Zibra AI - Liquids/Open Documentation");
        }

        private void TutorialsClick()
        {
            Application.OpenURL("https://bit.ly/3K113vk");
        }

        private void TutorialScenesClick()
        {
#if ZIBRA_LIQUID_FREE_VERSION
            Application.OpenURL(
                "https://zibra.ai/get-zibra-liquids-tutorials/?cgi_idsr_=&utm_source=tutorials&utm_campaign=zibra_asset_free");
#else
            Application.OpenURL("https://bit.ly/3c1zsh1");
#endif
        }

        private void FullVersionClick()
        {
            Application.OpenURL(
                "https://assetstore.unity.com/packages/tools/physics/zibra-liquids-200718?aid=1011lmkNI&pubref=freeversion");
        }
    }
}