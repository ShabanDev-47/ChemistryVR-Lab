using System;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using UnityEditor;
#endif

namespace com.zibra.liquid.DataStructures
{
    [ExecuteInEditMode]
    public class ZibraLiquidAdvancedRenderParameters : MonoBehaviour
    {
        [Tooltip("How much to downscale the ray march resolution. Can improve performance.")]
        [Range(0, 1)]
        public float RayMarchingResolutionDownscale = 1.0f;

        public enum LiquidRefractionQuality
        {
            PerVertexRender,
            PerPixelRender
        }

        [HideInInspector]
        [NonSerialized]
        [Obsolete("RefractionQuality is deprecated. Use RayMarchingResolutionDownscale instead.", true)]
        public LiquidRefractionQuality RefractionQuality = LiquidRefractionQuality.PerPixelRender;

        [SerializeField]
        [FormerlySerializedAs("RefractionQuality")]
        private LiquidRefractionQuality RefractionQualityOld;

        public enum RayMarchingBounces
        {
            SingleBounce,
            TwoBounces
        }

        [Tooltip("Number of bounces of the refraction ray, to see the liquid behind itself you need 2 bounces")]
        public RayMarchingBounces RefractionBounces = RayMarchingBounces.SingleBounce;

        [Tooltip("Enable underwater rendering. Disable it if you don't need it, since it's a bit slower.")]
        public bool UnderwaterRender = false;

        [Tooltip(
            "The maximum fraction of triangles the mesh can have (below 1.0f there is a chance of rendering only part of the mesh). Has a large effect on VRAM and performance.")]
        [Range(0.1f, 1.0f)]
        public float MaxLiquidMeshSize = 1.0f;

        [HideInInspector]
        [NonSerialized]
        [Obsolete("Particle Render is deprecated. AdditionalJFAIterations was only used in Particle Render.", true)]
        public int AdditionalJFAIterations = 0;

        [Tooltip("Number of iterations that move the mesh vertex to the liquid iso-surface")]
        [Range(0, 20)]
        public int VertexOptimizationIterations = 5;

        [Tooltip("Number of smoothing iterations for the mesh")]
        [Range(0, 8)]
        public int MeshOptimizationIterations = 2;

        [Tooltip(
            "This parameter moves liquid mesh vertices to be closer to the actual liquid surface. It should be manually fine tuned until you get a smooth mesh.")]
        [Range(0.0f, 2.0f)]
        public float VertexOptimizationStep = 0.82f;

        [Tooltip("The strength of the mesh smoothing per iteration")]
        [Range(0.0f, 1.0f)]
        public float MeshOptimizationStep = 0.91f;

        [Tooltip("The iso-value at which the mesh vertices are generated")]
        [Range(0.01f, 2.0f)]
        public float DualContourIsoSurfaceLevel = 0.025f;

        [Tooltip("Controls the position of the fluid surface. Lower values result in thicker surface.")]
        [Range(0.01f, 2.0f)]
        public float IsoSurfaceLevel = 0.36f;

        [Tooltip("The iso-surface level for the ray marching. Should be about 1-1/2 of the liquid density.")]
        [Range(0.0f, 5.0f)]
        public float RayMarchIsoSurface = 0.65f;

        [Tooltip("Maximum number of steps the ray can go, has a large effect on the performance")]
        [Range(4, 128)]
        public int RayMarchMaxSteps = 128;

        [Tooltip("Step size of the ray marching, controls accuracy, also has a large effect on performance")]
        [Range(0.0f, 1.0f)]
        public float RayMarchStepSize = 0.2f;

        [Tooltip(
            "Varies the ray marching step size, in some cases might improve performance by slightly reducing ray marching quality")]
        [Range(1.0f, 10.0f)]
        public float RayMarchStepFactor = 4.0f;

        [HideInInspector]
        [SerializeField]
        private int ObjectVersion = 1;

#if UNITY_EDITOR
        void OnSceneOpened(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            Debug.Log("Zibra Liquid Advanced Render Parameters format was updated. Please re-save scene.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        public void OnDestroy()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        }
#endif

        [ExecuteInEditMode]
        public void Awake()
        {
            // If Material Parameters is in old format we need to parse old parameters and come up with equivalent new
            // ones
            if (ObjectVersion == 1)
            {
                if (RefractionQualityOld == LiquidRefractionQuality.PerPixelRender)
                {
                    RayMarchingResolutionDownscale = 1.0f;
                }
                else
                {
                    RayMarchingResolutionDownscale = 0.5f;
                }

                ObjectVersion = 2;

#if UNITY_EDITOR
                // Can't mark object dirty in Awake, since scene is not fully loaded yet
                UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
#endif
            }
        }
    }
}