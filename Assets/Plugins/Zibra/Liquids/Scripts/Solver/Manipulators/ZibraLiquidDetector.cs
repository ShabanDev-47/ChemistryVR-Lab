#if ZIBRA_LIQUID_PAID_VERSION

using UnityEngine;
using System;
using com.zibra.liquid.SDFObjects;

#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

namespace com.zibra.liquid.Manipulators
{
    [AddComponentMenu("Zibra/Zibra Liquid Detector")]
    [DisallowMultipleComponent]
    public class ZibraLiquidDetector : Manipulator
    {
        [NonSerialized]
        public int particlesInside = 0;

        [HideInInspector]
        [SerializeField]
        private int ObjectVersion = 1;

        override public ManipulatorType GetManipulatorType()
        {
            return ManipulatorType.Detector;
        }

        [ExecuteInEditMode]
        public void Awake()
        {
#if UNITY_EDITOR
            bool updated = false;
#endif
            // If Emitter is in old format we need to parse old parameters and come up with equivalent new ones
            if (ObjectVersion == 1)
            {
                if (GetComponent<SDFObject>() == null)
                {
                    AnalyticSDF sdf = gameObject.AddComponent<AnalyticSDF>();
                    sdf.chosenSDFType = SDFObject.SDFType.Box;
#if UNITY_EDITOR
                    updated = true;
#endif
                }

                ObjectVersion = 2;
            }

#if UNITY_EDITOR
            if (updated)
            {
                // Can't mark object dirty in Awake, since scene is not fully loaded yet
                UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            }
#endif
        }

#if UNITY_EDITOR
        override public Color GetGizmosColor()
        {
            return Color.magenta;
        }

        void OnSceneOpened(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            Debug.Log("Zibra Liquid Detector format was updated. Please resave scene.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        void Reset()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            ObjectVersion = 2;
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

        public new void OnDestroy()
        {
            base.OnDestroy();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        }
#endif
    }
}

#endif