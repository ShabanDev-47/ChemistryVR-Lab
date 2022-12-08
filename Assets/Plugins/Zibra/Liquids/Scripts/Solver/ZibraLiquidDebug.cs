using AOT;
using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR
using UnityEngine;

namespace com.zibra.liquid.Solver
{
#if ZIBRA_LIQUID_DEBUG

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class ZibraLiquidDebug
    {
        public static string EditorPrefsKey = "ZibraLiquidsLogLevel";
        public static ZibraLiquidBridge.LogLevel CurrentLogLevel;

        public static void SetLogLevel(ZibraLiquidBridge.LogLevel level)
        {
            CurrentLogLevel = level;
#if UNITY_EDITOR
            EditorPrefs.SetInt(EditorPrefsKey, (int)level);
#endif // UNITY_EDITOR

            InitializeDebug();
        }
        static ZibraLiquidDebug()
        {
#if UNITY_EDITOR
            CurrentLogLevel =
                (ZibraLiquidBridge.LogLevel)EditorPrefs.GetInt(EditorPrefsKey, (int)ZibraLiquidBridge.LogLevel.Error);
#else
            CurrentLogLevel = ZibraLiquidBridge.LogLevel.Error;
#endif // UNITY_EDITOR
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        static void InitializeDebug()
        {
            DebugLogCallbackT callbackDelegate = new DebugLogCallbackT(DebugLogCallback);
            var settings = new ZibraLiquidBridge.LoggerSettings();
            settings.PFNCallback = Marshal.GetFunctionPointerForDelegate(callbackDelegate);
            settings.LogLevel = CurrentLogLevel;
            IntPtr settingsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(settings));
            Marshal.StructureToPtr(settings, settingsPtr, true);
            SetDebugLogWrapperPointer(settingsPtr);
            Marshal.FreeHGlobal(settingsPtr);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void DebugLogCallbackT(IntPtr message);
        [MonoPInvokeCallback(typeof(DebugLogCallbackT))]
        static void DebugLogCallback(IntPtr request)
        {
            ZibraLiquidBridge.DebugMessage message = Marshal.PtrToStructure<ZibraLiquidBridge.DebugMessage>(request);
            string text = Marshal.PtrToStringAnsi(message.Text);
            switch (message.Level)
            {
            case ZibraLiquidBridge.LogLevel.Verbose:
                Debug.Log("ZibraLiquid[verbose]: " + text);
                break;
            case ZibraLiquidBridge.LogLevel.Info:
                Debug.Log("ZibraLiquid: " + text);
                break;
            case ZibraLiquidBridge.LogLevel.Warning:
                Debug.LogWarning(text);
                break;
            case ZibraLiquidBridge.LogLevel.Performance:
                Debug.LogWarning("ZibraLiquid | Performance Warning:" + text);
                break;
            case ZibraLiquidBridge.LogLevel.Error:
                Debug.LogError("ZibraLiquid" + text);
                break;
            default:
                Debug.LogError("ZibraLiquid | Incorrect native log data format.");
                break;
            }
        }

        [DllImport(ZibraLiquidBridge.PluginLibraryName)]
        static extern void SetDebugLogWrapperPointer(IntPtr callback);
    }
#endif
}