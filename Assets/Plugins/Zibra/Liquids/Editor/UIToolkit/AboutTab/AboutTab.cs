using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using com.zibra.liquid.Foundation.UIElements;
#if ZIBRA_LIQUID_PAID_VERSION
using com.zibra.liquid.Editor.SDFObjects;
#endif

#if UNITY_2019_4_OR_NEWER
namespace com.zibra.liquid.Plugins.Editor
{
    internal class AboutTab : BaseTab
    {
        private VisualElement m_RegistrationBlock;

#if ZIBRA_LIQUID_PAID_VERSION
        private TextField m_AuthKeyInputField;
        private Button m_CheckAuthKeyBtn;
        private Button m_RegisterAuthKeyBtn;
        private Label m_InvalidKeyLabel;
        private Label m_RegisteredKeyLabel;
#endif

        const int KEY_LENGTH = 36;

        public AboutTab() : base($"{ZibraAIPackage.UIToolkitPath}/AboutTab/AboutTab")
        {
            m_RegistrationBlock = this.Q<SettingsBlock>("registrationBlock");
#if ZIBRA_LIQUID_PAID_VERSION
            m_AuthKeyInputField = this.Q<TextField>("authKeyInputField");
            m_CheckAuthKeyBtn = this.Q<Button>("validateAuthKeyBtn");
            m_RegisterAuthKeyBtn = this.Q<Button>("registerKeyBtn");
            m_InvalidKeyLabel = this.Q<Label>("invalidKeyLabel");
            m_RegisteredKeyLabel = this.Q<Label>("registeredKeyLabel");

            ZibraServerAuthenticationManager.GetInstance().Initialize();
            m_RegisterAuthKeyBtn.clicked += OnRegisterAuthKeyBtnOnClickedHandler;
            m_AuthKeyInputField.value = ZibraServerAuthenticationManager.GetInstance().PluginLicenseKey;
            m_CheckAuthKeyBtn.clicked += OnAuthKeyBtnOnClickedHandler;
            // Hide if key is valid.
            if (ZibraServerAuthenticationManager.GetInstance().GetStatus() ==
                ZibraServerAuthenticationManager.Status.OK)
            {
                m_RegisterAuthKeyBtn.style.display = DisplayStyle.None;
                m_CheckAuthKeyBtn.style.display = DisplayStyle.None;
                m_AuthKeyInputField.style.display = DisplayStyle.None;
                m_RegisteredKeyLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_RegisteredKeyLabel.style.display = DisplayStyle.None;
            }

            m_InvalidKeyLabel.style.display = DisplayStyle.None;

#else
            m_RegistrationBlock.style.display = DisplayStyle.None;
#endif
        }

#if ZIBRA_LIQUID_PAID_VERSION
        private void OnRegisterAuthKeyBtnOnClickedHandler()
        {
            Application.OpenURL("https://registration.zibra.ai/");
        }

        private void OnAuthKeyBtnOnClickedHandler()
        {
            string key = m_AuthKeyInputField.text.Trim();

            if (key.Length == KEY_LENGTH)
            {
                ZibraServerAuthenticationManager.GetInstance().RegisterKey(m_AuthKeyInputField.text);
                m_InvalidKeyLabel.style.display = DisplayStyle.None;
                m_RegisteredKeyLabel.style.display = DisplayStyle.None;
            }
            else
            {
                EditorUtility.DisplayDialog("Zibra Liquid Key Error", "Incorrect key format.", "Ok");
            }
        }
#endif
    }
}
#endif