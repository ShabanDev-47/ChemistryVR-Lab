using System;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using UnityEditor;
#endif

namespace com.zibra.liquid.DataStructures
{
    [ExecuteInEditMode]
    public class ZibraLiquidMaterialParameters : MonoBehaviour
    {
        [System.Serializable]
        public class LiquidMaterial
        {
            [Tooltip("The color of the liquid body")]
            public Color Color = new Color(0.3411765f, 0.92156863f, 0.85236126f, 1.0f);

            [ColorUsage(true, true)]
            public Color EmissiveColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

            [Tooltip(
                "The amount of light being scattered by the liquid volume. Visually adds a fog to the fluid volume. Maximum value makes the liquid opaque.")]
            [Range(0.0f, 100.0f)]
            public float ScatteringAmount = 5.0f;

            [Tooltip(
                "The amount of light absorbed in the liquid volume. Visually darkens all colors except to the selected liquid color.")]
            [Range(0.0f, 100.0f)]
            public float AbsorptionAmount = 20.0f;

            [Tooltip("The metalness of the surface")]
            [Range(0.0f, 1.0f)]
            public float Metalness = 0.3f;

            [Tooltip("The roughness of the surface")]
            [Range(0.0f, 1.0f)]
            public float Roughness = 0.3f;
        }

#if UNITY_EDITOR
        private static string DEFAULT_UPSCALE_MATERIAL_GIUD = "374557399a8cb1b499aee6a0cc226496";
        private static string DEFAULT_FLUID_MESH_MATERIAL_GIUD = "248b1858901577949a18bb8d09cb583f";
        private static string DEFAULT_SDF_RENDER_MATERIAL_GIUD = "a29ad26b5c6c24c43ba0cbdc686b6b41";
        private static string NO_OP_MATERIAL_GIUD = "248b1858901577949a18bb8d09cb583f";
#endif

        [Tooltip("Custom mesh fluid material.")]
        public Material FluidMeshMaterial;

        [Tooltip("Custom upscale material. Not used if you don't enable downscale in Liquid instance.")]
        public Material UpscaleMaterial;

        // Don't think anyone will need to edit this material
        // But if anyone will ever need that, removing [HideInInspector] will work
        [HideInInspector]
        public Material SDFRenderMaterial;

        [HideInInspector]
        public Material NoOpMaterial;

        [NonSerialized]
        [Obsolete("RefractionColor is deprecated. Use Color instead.", true)]
        public Color RefractionColor;

        [Tooltip("The color of the liquid body")]
        [FormerlySerializedAs("RefractionColor")]
        public Color Color = new Color(0.3411765f, 0.92156863f, 0.85236126f, 1.0f);

        [Tooltip("The color of the liquid reflection.")]
        [ColorUsage(true, true)]
#if UNITY_PIPELINE_HDRP
        public Color ReflectionColor = new Color(0.004434771f, 0.004434771f, 0.004434771f, 1.0f);
#else
        public Color ReflectionColor = new Color(1.39772f, 1.39772f, 1.39772f, 1.0f);
#endif

        [Tooltip("The emissive color of the liquid. Normally black for most liquids.")]
        [ColorUsage(true, true)]
        public Color EmissiveColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

#if ZIBRA_LIQUID_DEBUG
        [NonSerialized]
        public float NeuralSamplingDistance = 1.0f;
        [NonSerialized]
        public float SDFDebug = 0.0f;
#endif

        [NonSerialized]
        [HideInInspector]
        [Obsolete(
            "Smoothness is deprecated. Use Roughness instead. Roughness have inverted scale, i.e. Smoothness = 1.0 is equivalent to Roughness = 0.0",
            true)]
        public float Smoothness = 0.96f;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("Smoothness")]
        private float SmoothnessOld = 0.96f;

        [Range(0.0f, 1.0f)]
        public float Roughness = 0.04f;

        [NonSerialized]
        [Obsolete("Metal is deprecated. Use Metalness instead.", true)]
        public float Metal;

        [Tooltip("The metalness of the surface")]
        [FormerlySerializedAs("Metal")]
        [Range(0.0f, 1.0f)]
        public float Metalness = 0.3f;

        [Tooltip(
            "The amount of light being scattered by the liquid volume. Visually adds a fog to the fluid volume. Maximum value makes the liquid opaque.")]
        [Range(0.0f, 400.0f)]
        public float ScatteringAmount = 5.0f;

        [Tooltip(
            "The amount of light absorbed in the liquid volume. Visually darkens all colors except to the selected liquid color.")]
        [FormerlySerializedAs("Opacity")]
        [Range(0.0f, 400.0f)]
        public float AbsorptionAmount = 20.0f;

        [NonSerialized]
        [Obsolete("Opacity is deprecated. Use AbsorptionAmount instead.", true)]
        public float Opacity;

        [HideInInspector]
        [Obsolete("Shadowing is deprecated. We currently don't have correct shadowing effect.", true)]
        public float Shadowing;

        [NonSerialized]
        [Obsolete("RefractionDistort is deprecated. Use RefractionDistortion instead.", true)]
        public float RefractionDistort;

        [NonSerialized]
        [Obsolete(
            "RefractionDistortion is deprecated. Use IndexOfRefraction instead. Note that it have different scale.",
            true)]
        public float RefractionDistortion;

        [Tooltip("The index of refraction")]
        [Range(1.0f, 3.0f)]
        public float IndexOfRefraction = 1.333f;

        [Tooltip(
            "The radius of the blur of the liquid density on the simulation grid. Controls the smoothness of the normals.")]
        [Range(0.01f, 4.0f)]
        public float FluidSurfaceBlur = 1.5f;

        [HideInInspector]
        [SerializeField]
        private int ObjectVersion = 1;

#if UNITY_EDITOR
        void OnSceneOpened(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            Debug.Log("Zibra Liquid Material Parameters format was updated. Please resave scene.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        [ExecuteInEditMode]
        public void Awake()
        {
            // If Material Parameters is in old format we need to parse old parameters and come up with equivalent new
            // ones
#if UNITY_EDITOR
            bool updated = false;
#endif

            if (ObjectVersion == 1)
            {
                Roughness = 1 - SmoothnessOld;

                ObjectVersion = 2;
#if UNITY_EDITOR
                updated = true;
#endif
            }

            if (ObjectVersion == 2)
            {
                Solver.ZibraLiquid instance = GetComponent<Solver.ZibraLiquid>();

                // if not a newly created liquid instance
                //(material parameters are created before liquid)
                if (instance != null)
                {
                    const float TotalScale = 0.33f;
                    float SimulationScale =
                        TotalScale * (instance.containerSize.x + instance.containerSize.y + instance.containerSize.z);

                    ScatteringAmount *= SimulationScale;
                    AbsorptionAmount *= SimulationScale;
                }

                ObjectVersion = 3;

#if UNITY_EDITOR
                updated = true;
#endif
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
        public void OnDestroy()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        void Reset()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            ObjectVersion = 3;
            string DefaultUpscaleMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_UPSCALE_MATERIAL_GIUD);
            UpscaleMaterial = AssetDatabase.LoadAssetAtPath(DefaultUpscaleMaterialPath, typeof(Material)) as Material;
            string DefaultFluidMeshMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_FLUID_MESH_MATERIAL_GIUD);
            FluidMeshMaterial =
                AssetDatabase.LoadAssetAtPath(DefaultFluidMeshMaterialPath, typeof(Material)) as Material;
            string DefaultSDFRenderMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_SDF_RENDER_MATERIAL_GIUD);
            SDFRenderMaterial =
                AssetDatabase.LoadAssetAtPath(DefaultSDFRenderMaterialPath, typeof(Material)) as Material;
            string NoOpMaterialPath = AssetDatabase.GUIDToAssetPath(NO_OP_MATERIAL_GIUD);
            NoOpMaterial = AssetDatabase.LoadAssetAtPath(NoOpMaterialPath, typeof(Material)) as Material;
        }

        void OnValidate()
        {
            if (UpscaleMaterial == null)
            {
                string DefaultUpscaleMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_UPSCALE_MATERIAL_GIUD);
                UpscaleMaterial =
                    AssetDatabase.LoadAssetAtPath(DefaultUpscaleMaterialPath, typeof(Material)) as Material;
            }
            if (FluidMeshMaterial == null)
            {
                string DefaultFluidMeshMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_FLUID_MESH_MATERIAL_GIUD);
                FluidMeshMaterial =
                    AssetDatabase.LoadAssetAtPath(DefaultFluidMeshMaterialPath, typeof(Material)) as Material;
            }
            if (SDFRenderMaterial == null)
            {
                string DefaultSDFRenderMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_SDF_RENDER_MATERIAL_GIUD);
                SDFRenderMaterial =
                    AssetDatabase.LoadAssetAtPath(DefaultSDFRenderMaterialPath, typeof(Material)) as Material;
            }
            string NoOpMaterialPath = AssetDatabase.GUIDToAssetPath(NO_OP_MATERIAL_GIUD);
            NoOpMaterial = AssetDatabase.LoadAssetAtPath(NoOpMaterialPath, typeof(Material)) as Material;
        }
#endif
    }
}