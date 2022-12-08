using System;
using UnityEngine;
using UnityEngine.Serialization;
using com.zibra.liquid.SDFObjects;

#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using UnityEditor;
#endif

namespace com.zibra.liquid.Manipulators
{
    [AddComponentMenu("Zibra/Zibra Liquid Force Field")]
    // Multiple components of this type are allowed
    public class ZibraLiquidForceField : Manipulator
    {
        public enum ForceFieldType
        {
            Radial,
#if ZIBRA_LIQUID_PAID_VERSION
            Directional,
            Swirl
#endif
        }

        public enum ForceFieldShape
        {
            Sphere,
            Cube
        }

        public const float STRENGTH_DRAW_THRESHOLD = 0.001f;

#if !ZIBRA_LIQUID_PAID_VERSION
        [HideInInspector]
#endif
        public ForceFieldType Type = ForceFieldType.Radial;

        [HideInInspector]
        [Obsolete("Shape is deprecated. Add a SDF component instead.", true)]
        public ForceFieldShape Shape = ForceFieldShape.Sphere;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("Shape")]
        private ForceFieldShape ShapeOld = ForceFieldShape.Sphere;

        [Tooltip("The strength of the force acting on the liquid")]
        [Range(-1.0f, 4.0f)]
        public float Strength = 1.0f;
        [Tooltip("How fast does the force lose its strength with distance to the center")]
        [Range(0.01f, 10.0f)]
        public float DistanceDecay = 1.0f;
        [Tooltip("Distance where force field activates")]
        [Range(-10.0f, 10.0f)]
        public float DistanceOffset = 0.0f;

        [Tooltip("Disable applying forces inside the object")]
        public bool DisableForceInside = true;

        [Tooltip("Force vector of the directional force field")]
        public Vector3 ForceDirection = Vector3.up;

        override public ManipulatorType GetManipulatorType()
        {
            return ManipulatorType.ForceField;
        }

        private void Update()
        {
            AdditionalData0.x = (int)Type;
            AdditionalData0.y = Strength;
            AdditionalData0.z = DistanceDecay;
            AdditionalData0.w = DistanceOffset;

            AdditionalData1.x = ForceDirection.x;
            AdditionalData1.y = ForceDirection.y;
            AdditionalData1.z = ForceDirection.z;
            AdditionalData1.w = DisableForceInside ? 1.0f : 0.0f;
        }

        [HideInInspector]
        [SerializeField]
        private int ObjectVersion = 1;

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
                    switch (ShapeOld)
                    {
                    case ForceFieldShape.Cube:
                        sdf.chosenSDFType = SDFObject.SDFType.Box;
                        break;
                    case ForceFieldShape.Sphere:
                    default:
                        sdf.chosenSDFType = SDFObject.SDFType.Sphere;
                        break;
                    }
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
            return new Color(1.0f, 0.55f, 0.0f);
        }

        void OnSceneOpened(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            Debug.Log("Zibra Liquid Force Field format was updated. Please resave scene.");
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
        void OnDrawGizmosSelected()
        {
            if (!enabled)
            {
                return;
            }

            Gizmos.matrix = GetTransform();
            Gizmos.color = Handles.color = GetGizmosColor();

            if (Math.Abs(Strength) < STRENGTH_DRAW_THRESHOLD)
                return;
            switch (Type)
            {
                case ForceFieldType.Radial:
                    Utilities.GizmosHelper.DrawArrowsSphereRadial(Vector3.zero, Strength, 32);
                    break;
#if ZIBRA_LIQUID_PAID_VERSION
                case ForceFieldType.Directional:
                    Utilities.GizmosHelper.DrawArrowsSphereDirectional(Vector3.zero, Vector3.right * Strength, 32);
                    break;
                case ForceFieldType.Swirl:
                    Utilities.GizmosHelper.DrawArrowsSphereTangent(Vector3.zero, ForceDirection * Strength, 32);
                    break;
#endif
            }
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }
#endif
    }
}
