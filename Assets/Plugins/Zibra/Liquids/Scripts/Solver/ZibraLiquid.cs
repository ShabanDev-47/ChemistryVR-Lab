using com.zibra.liquid.DataStructures;
using com.zibra.liquid.Manipulators;
using com.zibra.liquid.SDFObjects;
using com.zibra.liquid.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if UNITY_EDITOR && ZIBRA_LIQUID_PAID_VERSION
using com.zibra.liquid.Editor.SDFObjects;
#endif

#if UNITY_PIPELINE_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif // UNITY_PIPELINE_HDRP

#if !ZIBRA_LIQUID_PAID_VERSION && !ZIBRA_LIQUID_FREE_VERSION
#error Missing plugin version definition
#endif

namespace com.zibra.liquid.Solver
{
    /// <summary>
    /// Main ZibraFluid solver component
    /// </summary>
    [AddComponentMenu("Zibra/Zibra Liquid")]
    [RequireComponent(typeof(ZibraLiquidMaterialParameters))]
    [RequireComponent(typeof(ZibraLiquidSolverParameters))]
    [RequireComponent(typeof(ZibraLiquidAdvancedRenderParameters))]
    [RequireComponent(typeof(ZibraManipulatorManager))]
    [ExecuteInEditMode] // Careful! This makes script execute in edit mode.
    // Use "EditorApplication.isPlaying" for play mode only check.
    // Encase this check and "using UnityEditor" in "#if UNITY_EDITOR" preprocessor directive to prevent build errors
    public class ZibraLiquid : MonoBehaviour
    {
        // PluginVersionStandard must follow C# versioning standard (d.d.d.d)
        public const string PluginVersionStandard = "1.5.0.0";
        public const string PluginVersion = "1.5.0";

        /// <summary>
        /// A list of all instances of the ZibraFluid solver
        /// </summary>
        public static List<ZibraLiquid> AllFluids = new List<ZibraLiquid>();

        public static int ms_NextInstanceId = 0;
        public const int MPM_THREADS = 256;
        public const int RADIX_THREADS = 128;
        public const int HISTO_WIDTH = 32;
        public const int ADDITIONAL_VERTICES = 3000;

        public const int STATISTICS_PER_MANIPULATOR = 8;

        // Unique ID that always present in each baked state asset
        public const int BAKED_LIQUID_HEADER_VALUE = 0x071B9AA1;

        // private const int DENSITY_COMPUTE_BLOCK = 5;

#if UNITY_PIPELINE_URP
        static int upscaleColorTextureID = Shader.PropertyToID("Zibra_DownscaledLiquidColor");
        static int upscaleDepthTextureID = Shader.PropertyToID("Zibra_DownscaledLiquidDepth");
#endif

#if UNITY_EDITOR
        // Used to update editors
        public event Action onChanged;
        public void NotifyChange()
        {
            if (onChanged != null)
            {
                onChanged.Invoke();
            }
        }
#endif

#region PARTICLES
        [StructLayout(LayoutKind.Sequential)]
        private class UnityTextureBridge
        {
            public IntPtr texture;
            public ZibraLiquidBridge.TextureFormat format;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterParticlesBuffersBridgeParams
        {
            public IntPtr PositionMass;
            public IntPtr AffineVelocity0;
            public IntPtr AffineVelocity1;
            public IntPtr ParticleNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class InitializeGPUReadbackParams
        {
            public UInt32 readbackBufferSize;
            public Int32 maxFramesInFlight;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TextureUploadData
        {
            public IntPtr data;
            public Int32 dataSize;
            public Int32 rowPitch;
            public Int32 dimensionX;
            public Int32 dimensionY;
            public Int32 dimensionZ;
        };

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterManipulatorsBridgeParams
        {
            public Int32 ManipulatorNum;
            public IntPtr ManipulatorBufferDynamic;
            public IntPtr SDFObjectBuffer;
            public IntPtr ManipulatorBufferStatistics;
            public IntPtr ManipulatorParams;
            public Int32 SDFObjectCount;
            public IntPtr SDFObjectData;
            public IntPtr ManipIndices;
            public UnityTextureBridge EmbeddingsTexture;
            public UnityTextureBridge SDFGridTexture;
            public TextureUploadData EmbeddigsData;
            public TextureUploadData SDFGridData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterSolverBuffersBridgeParams
        {
            public IntPtr SimulationParams;
            public IntPtr PositionMassCopy;
            public IntPtr GridData;
            public IntPtr IndexGrid;
            public IntPtr GridBlur0;
            public IntPtr GridBlur1;
            public IntPtr GridNormal;
            public IntPtr GridSDF;
            public IntPtr NodeParticlePairs0;
            public IntPtr NodeParticlePairs1;
            public IntPtr RadixGroupData1;
            public IntPtr RadixGroupData2;
            public IntPtr RadixGroupData3;
            public IntPtr Counters;
            public IntPtr VertexIDGrid;
            public IntPtr VertexBuffer0;
            public IntPtr VertexBuffer1;
            public IntPtr QuadBuffer;
            public IntPtr TransferDataBuffer;
            public IntPtr MeshRenderIndexBuffer;
            public IntPtr ParticleSpeciesData;
            public Int32 ParticleSpeciesCount;
            public IntPtr UnityMeshVertexBuffer;
            public IntPtr UnityMeshIndexBuffer;
            public IntPtr VertexData;
            public UnityTextureBridge GridNormals;
            public UnityTextureBridge GridDensity;
            public UnityTextureBridge GridVelocity;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RegisterRenderResourcesBridgeParams
        {
            public UnityTextureBridge Depth;
            public UnityTextureBridge Color0;
            public UnityTextureBridge Color1;
            public UnityTextureBridge Color2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class CameraParams
        {
            public Matrix4x4 View;
            public Matrix4x4 Projection;
            public Matrix4x4 ProjectionInverse;
            public Matrix4x4 ViewProjection;
            public Matrix4x4 ViewProjectionInverse;
            public Matrix4x4 EyeRayCameraCoeficients;
            public Vector3 WorldSpaceCameraPos;
            public Int32 CameraID;
            public Vector2 CameraResolution;
            Single CameraParamsPadding1;
            Single CameraParamsPadding2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class MeshRenderGlobalParams
        {
            public Vector3 RenderingParameterPadding1;
            public Single LiquidIOR;

            public Single RayMarchIsoSurface;
            public Int32 UnderwaterRender;
            public Single RayMarchStepSize;
            public Single RayMarchStepFactor;

            public Int32 RayMarchMaxSteps;
            public Int32 TwoBouncesEnabled;
            public Vector2Int RayMarchResolution;

            public Single FoamingIntensity;
            public Single FoamingDecay;
            public Single FoamingThreshold;
            public Single TextureDistortion;

            public Vector4 Absorption;

            public Single FoamRepeatPeriod;
            public Single FoamScale;
            public Single FoamAmplitude;
            public Single FoamFBM;

            public Single FoamBlurring;
            public Int32 EnableFoamTexture;
            public Vector2 RenderingParameterPadding2;
        };

        [StructLayout(LayoutKind.Sequential)]
        private class RenderParams
        {
            public Single BlurRadius;
            public Single RenderParamsPadding1;
            public Single NeuralSamplingDistance;
            public Single SDFDebug;

            public Int32 RenderingMode;
            public Int32 VertexOptimizationIterations;
            public Int32 MeshOptimizationIterations;
            public Single DualContourIsoValue;

            public Single MeshOptimizationStep;
            public Single CameraDensity;
            public Int32 MaxVertexBufferSize;
            public Int32 MaxIndexBufferSize;
        }
        public struct MaterialPair
        {
            public Material currentMaterial;
            public Material sharedMaterial;

            // Returns true if dirty
            public bool SetMaterial(Material mat)
            {
                if (sharedMaterial != mat)
                {
                    currentMaterial = (mat != null ? Material.Instantiate(mat) : null);
                    sharedMaterial = mat;
                    return true;
                }
                return false;
            }
        }

        public class CameraResources
        {
            public RenderTexture background;
            public MaterialPair liquidMaterial;
            public MaterialPair upscaleMaterial;
            public MaterialPair sdfRenderMaterial;
            public bool isDirty = true;
        }

        [NonSerialized]
        public RenderTexture color0;
        [NonSerialized]
        public RenderTexture color1;
        [NonSerialized]
        public RenderTexture color2;
        [NonSerialized]
        public RenderTexture upscaleColor;
        [NonSerialized]
        public RenderTexture depth;

        // TODO: Test LDS atomics + global atomic vs AppendBuffer performance
        [NonSerialized]
        public ComputeBuffer Counters;

        // [NonSerialized]
        // public ComputeBuffer IndirectArguments;
        [NonSerialized]
        public ComputeBuffer VertexIDGrid;
        [NonSerialized]
        public GraphicsBuffer VertexBuffer0;
        [NonSerialized]
        public GraphicsBuffer VertexBuffer1;
        [NonSerialized]
        public ComputeBuffer QuadBuffer;
        [NonSerialized]
        // Buffer for transferring data between incompatible buffer types.
        public ComputeBuffer TransferDataBuffer;
        [NonSerialized]
        public GraphicsBuffer MeshRenderIndexBuffer;
        [NonSerialized]
        public GraphicsBuffer VertexProperties;
        [NonSerialized]
        public Mesh LiquidMesh;
        [NonSerialized]
        public RenderTexture GridNormalTexture;
        [NonSerialized]
        public RenderTexture DensityTexture;
        [NonSerialized]
        public RenderTexture VelocityTexture;

        [NonSerialized]
        private Vector2Int CurrentTextureResolution = new Vector2Int(0, 0);

        // List of all cameras we have added a command buffer to
        private readonly Dictionary<Camera, CommandBuffer> cameraCBs = new Dictionary<Camera, CommandBuffer>();

        // Each camera needs its own resources
        List<Camera> cameras = new List<Camera>();

        public Dictionary<Camera, CameraResources> cameraResources = new Dictionary<Camera, CameraResources>();

        public Dictionary<Camera, IntPtr> camNativeParams = new Dictionary<Camera, IntPtr>();
        Dictionary<Camera, IntPtr> camMeshRenderParams = new Dictionary<Camera, IntPtr>();
        Dictionary<Camera, Vector2Int> camRenderResolutions = new Dictionary<Camera, Vector2Int>();
        Dictionary<Camera, Vector2Int> camNativeResolutions = new Dictionary<Camera, Vector2Int>();

#if ZIBRA_LIQUID_PAID_VERSION
        [Range(1024, 10000000)]
#else
        // Increasing this limit won't allow you to spawn more particles
        [Range(1024, 2097152)]
#endif
        public int MaxNumParticles = 262144;

        public ComputeBuffer PositionMass { get; private set; }
        public ComputeBuffer Velocity { get; private set; }
        public ComputeBuffer[] Affine { get; private set; }
        public ComputeBuffer ParticleNumber { get; private set; }
        [NonSerialized]
        public bool isEnabled = true;
        [NonSerialized]
        public float particleDiameter = 0.0f;
        [NonSerialized]
        public float particleMass = 1.0f;
        public Bounds bounds;

        // If set to false resolution is always 100%
        // If set to true DownscaleFactor is applied to liquid rendering
        public bool EnableDownscale = false;

        // Scale width/height of liquid render target
        // Pixel count is decreased by factor of DownscaleFactor * DownscaleFactor
        // So DownscaleFactor of 0.7 result in about 50% less pixels in render target
        // Doesn't have any effect unless EnableDownscale is set to true
        [Range(0.2f, 0.99f)]
        public float DownscaleFactor = 0.5f;

        private bool usingCustomReflectionProbe;

        private CameraParams cameraRenderParams;
        private MeshRenderGlobalParams meshRenderGlobalParams;
        private RenderParams renderParams;

#endregion

#region SOLVER

#if ZIBRA_LIQUID_PAID_VERSION
        /// <summary>
        /// Types of initial conditions
        /// </summary>
        public enum InitialStateType
        {
            NoParticles,
            BakedLiquidState
        }

        [Serializable]
        public class BakedInitialState
        {
            [SerializeField]
            public int ParticleCount;

            [SerializeField]
            public Vector4[] Positions;

            [SerializeField]
            public Vector2Int[] AffineVelocity;
        }

        public InitialStateType InitialState = InitialStateType.NoParticles;

        [Tooltip("Baked state saved with Baking Utility. Will reset to None if incompatible file is detected.")]
        public TextAsset BakedInitialStateAsset;
#endif

        /// <summary>
        /// Native solver instance ID number
        /// </summary>
        [NonSerialized]
        public int CurrentInstanceID;

        [StructLayout(LayoutKind.Sequential)]
        private class SimulationParams
        {
            public Vector3 GridSize;
            public Int32 ParticleCount;

            public Vector3 ContainerScale;
            public Int32 NodeCount;

            public Vector3 ContainerPos;
            public Single TimeStep;

            public Int32 SimulationFrame;
            public Single DensityBlurRadius;
            public Single LiquidIsosurfaceThreshold;
            public Single VertexOptimizationStep;

            public Vector3 ParticleTranslation;
            public Single GlobalVelocityLimit;

            public Single MinimumVelocity;
            public Single BlurNormalizationConstant;
            public Int32 MaxParticleCount;
            public Int32 VisualizeSDF;

            public Single SimulationTime;
            public Single FoamBuoyancy;
            public Int32 ParticleSpeciesCount;
            public Single SimulationParameterPadding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class ParticleSpeciesParameters
        {
            public Vector3 Gravity;
            public Single AffineAmmount;

            public Single LiquidStiffness;
            public Single RestDensity;
            public Single SurfaceTension;
            public Single AffineDivergenceDecay;

            public Vector3 Material;
            public Single VelocityLimit;
        }

        private const int BlockDim = 8;
        public ComputeBuffer GridData { get; private set; }
        public ComputeBuffer IndexGrid { get; private set; }
        public ComputeBuffer GridBlur0 { get; private set; }
        public ComputeBuffer GridNormal { get; private set; }
        public ComputeBuffer GridSDF { get; private set; }
        public ComputeBuffer SurfaceGridType { get; private set; }
        public Texture3D SDFGridTexture { get; private set; }
        public Texture3D EmbeddingsTexture { get; private set; }

        /// <summary>
        /// Current timestep
        /// </summary>
        public float timestep = 0.0f;

        /// <summary>
        /// Simulation time passed (in simulation time units)
        /// </summary>
        public float simulationInternalTime { get; private set; } = 0.0f;

        /// <summary>
        /// Number of simulation iterations done so far
        /// </summary>
        public int simulationInternalFrame { get; private set; } = 0;

        private int numNodes = 0;
        private SimulationParams fluidParameters;
        private ComputeBuffer positionMassCopy;
        private ComputeBuffer GridBlur1;
        private ComputeBuffer nodeParticlePairs0;
        private ComputeBuffer nodeParticlePairs1;
        private ComputeBuffer RadixGroupData1;
        private ComputeBuffer RadixGroupData2;
        private ComputeBuffer RadixGroupData3;

        private CommandBuffer solverCommandBuffer;

        private List<IntPtr> toFreeOnExit = new List<IntPtr>();

#endregion

        public enum RenderingMode
        {
            [Obsolete("Particle Render is deprecated. Please use Mesh Render.", true)]
            ParticleRender = 0,
            MeshRender = 1,
            UnityRender = 2
        }

        // Don't change this parameter on active liquid
        // It won't work in future versions
        public RenderingMode CurrentRenderingMode = RenderingMode.MeshRender;
        private RenderingMode ActiveRenderingMode = RenderingMode.MeshRender;

        // Only used on SRP
        public CameraEvent CurrentInjectionPoint = CameraEvent.BeforeForwardAlpha;
        private CameraEvent ActiveInjectionPoint = CameraEvent.BeforeForwardAlpha;

        public bool IsSimulatingInBackground { get; set; }

        /// <summary>
        /// The grid size of the simulation
        /// </summary>
        public Vector3Int GridSize { get; private set; }

        [NonSerialized]
        [Obsolete(
            "reflectionProbe is deprecated. Use reflectionProbeSRP or reflectionProbeHDRP instead depending on your Rendering Pipeline (URP uses reflectionProbeSRP).",
            true)]
        public ReflectionProbe reflectionProbe;

#if UNITY_PIPELINE_HDRP
        [FormerlySerializedAs("reflectionProbe")]
        [Tooltip("Use a custom reflection probe")]
        public HDProbe reflectionProbeHDRP;
        [Tooltip("Use a custom light")]
        public Light customLightHDRP;
#else
        [FormerlySerializedAs("reflectionProbe")]
#endif // UNITY_PIPELINE_HDRP
        [Tooltip("Use a custom reflection probe")]
        public ReflectionProbe reflectionProbeSRP;

        [Tooltip("The maximum allowed simulation timestep")]
        [Range(0.0f, 1.0f)]
        public float timeStepMax = 1.00f;

        [Tooltip("Fallback max frame latency. Used when it isn't possible to retrieve Unity's max frame latency.")]
        [Range(2, 16)]
        public UInt32 maxFramesInFlight = 3;

        [Tooltip("The speed of the simulation, how many simulation time units per second")]
        [Range(0.0f, 100.0f)]
        public float simTimePerSec = 40.0f;

        public int activeParticleNumber { get; private set; } = 0;

        [Tooltip("The number of solver iterations per frame, in most cases one iteration is sufficient")]
        [Range(1, 10)]
        public int iterationsPerFrame = 1;

        public float CellSize { get; private set; }

        [Tooltip("Sets the resolution of the largest sid of the grids container equal to this value")]
        [Min(16)]
        public int gridResolution = 128;

        [Range(1e-2f, 16.0f)]
        public float emitterDensity = 1.0f;

        public bool runSimulation = true;
        public bool runRendering = true;

        public bool visualizeSceneSDF = false;

        /// <summary>
        /// Main parameters of the simulation
        /// </summary>
        public ZibraLiquidSolverParameters solverParameters;

        /// <summary>
        /// Main rendering parameters
        /// </summary>
        public ZibraLiquidMaterialParameters materialParameters;

        /// <summary>
        /// Advanced rendering parameters
        /// </summary>
        public ZibraLiquidAdvancedRenderParameters renderingParameters;

        /// <summary>
        /// Solver container size
        /// </summary>
        public Vector3 containerSize = new Vector3(10, 10, 10);

        /// <summary>
        /// Solver container position
        /// </summary>
        public Vector3 containerPos;

        /// <summary>
        /// Initial velocity of the fluid
        /// </summary>
        public Vector3 fluidInitialVelocity;

        /// <summary>
        /// Manager for all objects interacting in some way with the simulation
        /// </summary>
        [HideInInspector]
        [SerializeField]
        public ZibraManipulatorManager manipulatorManager;

        private IntPtr NativeManipData;
        private IntPtr NativeSDFData;
        private IntPtr NativeFluidData;
        private IntPtr NativeSolverData;

        /// <summary>
        /// Compute buffer with dynamic manipulator data
        /// </summary>
        public ComputeBuffer DynamicManipulatorData { get; private set; }

        /// <summary>
        /// Compute buffer with constant manipulator data
        /// </summary>
        public ComputeBuffer SDFObjectData { get; private set; }

        /// <summary>
        /// Compute buffer with statistics about the manipulators
        /// </summary>
        public ComputeBuffer ManipulatorStatistics { get; private set; }

        /// <summary>
        /// Compute buffer with particle species data
        /// </summary>
        public ComputeBuffer ParticleSpeciesData { get; private set; }

        /// <summary>
        /// List of used SDF colliders
        /// </summary>
        [SerializeField]
        private List<ZibraLiquidCollider> sdfColliders = new List<ZibraLiquidCollider>();

        /// <summary>
        /// List of used manipulators
        /// </summary>
        [SerializeField]
        private List<Manipulator> manipulators = new List<Manipulator>();

        public int avgFrameRate;
        public float deltaTime;
        public float smoothDeltaTime;

        public bool forceTextureUpdate = false;

        /// <summary>
        /// Is solver initialized
        /// </summary>
        //[NonSerialized]
        public bool initialized { get; private set; } = false;

        /// <summary>
        /// Is solver using fixed unity time steps
        /// </summary>
        public bool useFixedTimestep = false;

#if UNITY_EDITOR
        private bool ForceRepaint = false;
#endif

#if UNITY_PIPELINE_HDRP
        private LiquidHDRPRenderComponent hdrpRenderer;
#endif // UNITY_PIPELINE_HDRP

        enum GraphicsBufferType
        {
            Vertex,
            Index
        }

        GraphicsBuffer CreateGraphicsBuffer(GraphicsBufferType type, int count, int stride)
        {
            // Unity 2019 don't have UAV in graphics buffers
            // So we have to create them internally
#if UNITY_2020_1_OR_NEWER
            return new GraphicsBuffer(type == GraphicsBufferType.Vertex
                                          ? GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Vertex
                                          : GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Index,
                                      count, stride);
#else
            return null;
#endif
        }

        IntPtr GetNativePtr(ComputeBuffer buffer)
        {
            return buffer == null ? IntPtr.Zero : buffer.GetNativeBufferPtr();
        }

        IntPtr GetNativePtr(GraphicsBuffer buffer)
        {
            return buffer == null ? IntPtr.Zero : buffer.GetNativeBufferPtr();
        }

        IntPtr GetNativePtr(RenderTexture texture)
        {
            return texture == null ? IntPtr.Zero : texture.GetNativeTexturePtr();
        }

        IntPtr GetNativePtr(Texture3D texture)
        {
            return texture == null ? IntPtr.Zero : texture.GetNativeTexturePtr();
        }

        public bool IsRenderingEnabled()
        {
            // We need at least 2 simulation frames before we can start rendering
            return initialized && runRendering && (simulationInternalFrame > 1) &&
                   (CurrentRenderingMode != RenderingMode.UnityRender || visualizeSceneSDF);
        }

        public bool IsSimulationEnabled()
        {
            // We need at least 2 simulation frames before we can start rendering
            // So we need to always simulate first 2 frames
            return initialized && (runSimulation || (simulationInternalFrame <= 2));
        }

        /// <summary>
        /// Activate the solver
        /// </summary>
        public void Run()
        {
            runSimulation = true;
        }

        /// <summary>
        /// Stop the solver
        /// </summary>
        public void Stop()
        {
            runSimulation = false;
        }

        void SetupScriptableRenderComponents()
        {
#if UNITY_PIPELINE_HDRP
#if UNITY_EDITOR
            if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
            {
                hdrpRenderer = gameObject.GetComponent<LiquidHDRPRenderComponent>();
                if (hdrpRenderer == null)
                {
                    hdrpRenderer = gameObject.AddComponent<LiquidHDRPRenderComponent>();
                    hdrpRenderer.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
                    hdrpRenderer.AddPassOfType(typeof(LiquidHDRPRenderComponent.FluidHDRPRender));
                    LiquidHDRPRenderComponent.FluidHDRPRender renderer =
                        hdrpRenderer.customPasses[0] as LiquidHDRPRenderComponent.FluidHDRPRender;
                    renderer.name = "ZibraLiquidRenderer";
                    renderer.liquid = this;
                }
            }
#endif
#endif // UNITY_PIPELINE_HDRP
        }

        void ForceCloseCommandEncoder(CommandBuffer cmdList)
        {
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && UNITY_STANDALONE_OSX)
            // Unity bug workaround
            // For whatever reason, Unity sometimes doesn't close command encoder when we request it from native plugin
            // So when we try to start our command encoder with active encoder already present it leads to crash
            // This happens when scene have Terrain (I still have no idea why)
            // So we force change command encoder like that, and this one closes gracefuly
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
            {
                cmdList.SetRenderTarget(color0);
                cmdList.DrawProcedural(new Matrix4x4(), materialParameters.NoOpMaterial, 0, MeshTopology.Triangles, 3);
            }
#endif
        }

        UnityTextureBridge MakeTextureNativeBridge(RenderTexture texture)
        {
            var unityTextureBridge = new UnityTextureBridge();
            if (texture != null)
            {
                unityTextureBridge.texture = GetNativePtr(texture);
                unityTextureBridge.format = ZibraLiquidBridge.ToBridgeTextureFormat(texture.graphicsFormat);
            }
            else
            {
                unityTextureBridge.texture = IntPtr.Zero;
                unityTextureBridge.format = ZibraLiquidBridge.TextureFormat.None;
            }

            return unityTextureBridge;
        }

        UnityTextureBridge MakeTextureNativeBridge(Texture3D texture)
        {
            var unityTextureBridge = new UnityTextureBridge();
            unityTextureBridge.texture = GetNativePtr(texture);
            unityTextureBridge.format = ZibraLiquidBridge.ToBridgeTextureFormat(texture.graphicsFormat);

            return unityTextureBridge;
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, containerSize);
            Gizmos.color = new Color(0.2f, 0.8f, 0.8f);
            Vector3 voxelSize =
                new Vector3(containerSize.x / GridSize.x, containerSize.y / GridSize.y, containerSize.z / GridSize.z);
            const int GizmosVoxelCubeSize = 2;
            for (int i = -GizmosVoxelCubeSize; i <= GizmosVoxelCubeSize; i++)
                for (int j = -GizmosVoxelCubeSize; j <= GizmosVoxelCubeSize; j++)
                    for (int k = -GizmosVoxelCubeSize; k <= GizmosVoxelCubeSize; k++)
                        Gizmos.DrawWireCube(transform.position +
                                                new Vector3(i * voxelSize.x, j * voxelSize.y, k * voxelSize.z),
                                            voxelSize);
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }

        void Start()
        {
            materialParameters = gameObject.GetComponent<ZibraLiquidMaterialParameters>();
            solverParameters = gameObject.GetComponent<ZibraLiquidSolverParameters>();
            renderingParameters = gameObject.GetComponent<ZibraLiquidAdvancedRenderParameters>();
            manipulatorManager = gameObject.GetComponent<ZibraManipulatorManager>();
        }

        protected void OnEnable()
        {
            SetupScriptableRenderComponents();

#if UNITY_EDITOR
#if ZIBRA_LIQUID_PAID_VERSION
            if (!ZibraLiquidBridge.IsPaidVersion())
            {
                Debug.LogError(
                    "Free version of native plugin used with paid version of C# plugin. If you just replaced your Zibra Liquids version you need to restart Unity Editor.");
            }
#else
            if (ZibraLiquidBridge.IsPaidVersion())
            {
                Debug.LogError(
                    "Paid version of native plugin used with free version of C# plugin. If you just replaced your Zibra Liquids version you need to restart Unity Editor.");
            }
#endif
#endif

            AllFluids?.Add(this);

            UpdateUnityRender();

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return;
            }
#endif

            Init();
        }

        public void UpdateGridSize()
        {
            CellSize = Math.Max(containerSize.x, Math.Max(containerSize.y, containerSize.z)) / gridResolution;

            GridSize = Vector3Int.CeilToInt(containerSize / CellSize);
        }

        private void InitializeParticles()
        {
            UpdateGridSize();

            fluidParameters = new SimulationParams();

            NativeFluidData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SimulationParams)));
            NativeSolverData =
                Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ParticleSpeciesParameters)));

            isEnabled = true;
            var numParticlesRounded =
                (int)Math.Ceiling((double)MaxNumParticles / MPM_THREADS) * MPM_THREADS; // round to workgroup size

            PositionMass = new ComputeBuffer(MaxNumParticles, 4 * sizeof(float));
            Affine = new ComputeBuffer[2];
            Affine[0] = new ComputeBuffer(4 * numParticlesRounded, 2 * sizeof(int));
            Affine[1] = new ComputeBuffer(4 * numParticlesRounded, 2 * sizeof(int));
            ParticleNumber = new ComputeBuffer(128, sizeof(int));
            int[] particleNumberInitialData = new int[128];
            ParticleNumber.SetData(particleNumberInitialData);

#if ZIBRA_LIQUID_DEBUG
            PositionMass.name = "PositionMass";
            Affine[0].name = "Affine0";
            Affine[1].name = "Affine1";
            ParticleNumber.name = "ParticleNumber";
#endif

#if ZIBRA_LIQUID_PAID_VERSION
            // We mush apply state before we send buffers to native plugin
            // SetData seems to recreate buffers at least on Metal
            ApplyInitialState();
#endif

            int[] Pnums = new int[128];
            for (int i = 0; i < 128; i++)
            {
                Pnums[i] = 0;
            }

            ParticleNumber.SetData(Pnums);

            if (manipulatorManager != null)
            {
                manipulatorManager.UpdateConst(manipulators, sdfColliders);
                manipulatorManager.UpdateDynamic(this);

                if (manipulatorManager.TextureCount > 0)
                {
                    EmbeddingsTexture = new Texture3D(
                        manipulatorManager.EmbeddingTextureDimension, manipulatorManager.EmbeddingTextureDimension,
                        manipulatorManager.EmbeddingTextureDimension, TextureFormat.RGBA32, false);

                    SDFGridTexture =
                        new Texture3D(manipulatorManager.SDFTextureDimension, manipulatorManager.SDFTextureDimension,
                                      manipulatorManager.SDFTextureDimension, TextureFormat.RHalf, false);

                    EmbeddingsTexture.filterMode = FilterMode.Trilinear;
                    SDFGridTexture.filterMode = FilterMode.Trilinear;
                }
                else
                {
                    EmbeddingsTexture = new Texture3D(1, 1, 1, TextureFormat.RGBA32, 0);
                    SDFGridTexture = new Texture3D(1, 1, 1, TextureFormat.RHalf, 0);
                }

                int ManipSize = Marshal.SizeOf(typeof(ZibraManipulatorManager.ManipulatorParam));
                int SDFSize = Marshal.SizeOf(typeof(ZibraManipulatorManager.SDFObjectParams));
                // Need to create at least some buffer to bind to shaders
                NativeManipData = Marshal.AllocHGlobal(manipulatorManager.Elements * ManipSize);
                NativeSDFData = Marshal.AllocHGlobal(manipulatorManager.SDFObjectList.Count * SDFSize);
                DynamicManipulatorData = new ComputeBuffer(Math.Max(manipulatorManager.Elements, 1), ManipSize);

                SDFObjectData = new ComputeBuffer(Math.Max(manipulatorManager.SDFObjectList.Count, 1),
                                                  Marshal.SizeOf(typeof(ZibraManipulatorManager.SDFObjectParams)));
                int ManipulatorStatisticsSize = Math.Max(STATISTICS_PER_MANIPULATOR * manipulatorManager.Elements, 1);
                ManipulatorStatistics = new ComputeBuffer(ManipulatorStatisticsSize, sizeof(int));
                int[] manipulatorStatisticsSizeInitialData = new int[ManipulatorStatisticsSize];
                ManipulatorStatistics.SetData(manipulatorStatisticsSizeInitialData);

#if ZIBRA_LIQUID_DEBUG
                DynamicManipulatorData.name = "DynamicManipulatorData";
                SDFObjectData.name = "SDFObjectData";
                ManipulatorStatistics.name = "ManipulatorStatistics";
#endif
                var gcparamBuffer2 = GCHandle.Alloc(manipulatorManager.indices, GCHandleType.Pinned);

                UpdateInteropBuffers();

                var registerManipulatorsBridgeParams = new RegisterManipulatorsBridgeParams();
                registerManipulatorsBridgeParams.ManipulatorNum = manipulatorManager.Elements;
                registerManipulatorsBridgeParams.ManipulatorBufferDynamic = GetNativePtr(DynamicManipulatorData);
                registerManipulatorsBridgeParams.SDFObjectBuffer = GetNativePtr(SDFObjectData);
                registerManipulatorsBridgeParams.ManipulatorBufferStatistics =
                    ManipulatorStatistics.GetNativeBufferPtr();
                registerManipulatorsBridgeParams.ManipulatorParams = NativeManipData;
                registerManipulatorsBridgeParams.SDFObjectCount = manipulatorManager.SDFObjectList.Count;
                registerManipulatorsBridgeParams.SDFObjectData = NativeSDFData;
                registerManipulatorsBridgeParams.ManipIndices = gcparamBuffer2.AddrOfPinnedObject();
                registerManipulatorsBridgeParams.EmbeddingsTexture = MakeTextureNativeBridge(EmbeddingsTexture);
                registerManipulatorsBridgeParams.SDFGridTexture = MakeTextureNativeBridge(SDFGridTexture);

                GCHandle embeddingDataHandle = default(GCHandle);
                if (manipulatorManager.Embeddings.Length > 0)
                {
                    embeddingDataHandle = GCHandle.Alloc(manipulatorManager.Embeddings, GCHandleType.Pinned);
                    registerManipulatorsBridgeParams.EmbeddigsData.dataSize =
                        Marshal.SizeOf(new Color32()) * manipulatorManager.Embeddings.Length;
                    registerManipulatorsBridgeParams.EmbeddigsData.data = embeddingDataHandle.AddrOfPinnedObject();
                    registerManipulatorsBridgeParams.EmbeddigsData.rowPitch =
                        Marshal.SizeOf(new Color32()) * EmbeddingsTexture.width;
                    registerManipulatorsBridgeParams.EmbeddigsData.dimensionX = EmbeddingsTexture.width;
                    registerManipulatorsBridgeParams.EmbeddigsData.dimensionY = EmbeddingsTexture.height;
                    registerManipulatorsBridgeParams.EmbeddigsData.dimensionZ = EmbeddingsTexture.depth;
                }

                GCHandle sdfGridHandle = default(GCHandle);
                if (manipulatorManager.SDFGrid.Length > 0)
                {
                    sdfGridHandle = GCHandle.Alloc(manipulatorManager.SDFGrid, GCHandleType.Pinned);
                    registerManipulatorsBridgeParams.SDFGridData.dataSize =
                        Marshal.SizeOf(new byte()) * manipulatorManager.SDFGrid.Length;
                    registerManipulatorsBridgeParams.SDFGridData.data = sdfGridHandle.AddrOfPinnedObject();
                    registerManipulatorsBridgeParams.SDFGridData.rowPitch =
                        Marshal.SizeOf(new byte()) * 2 * SDFGridTexture.width;
                    registerManipulatorsBridgeParams.SDFGridData.dimensionX = SDFGridTexture.width;
                    registerManipulatorsBridgeParams.SDFGridData.dimensionY = SDFGridTexture.height;
                    registerManipulatorsBridgeParams.SDFGridData.dimensionZ = SDFGridTexture.depth;
                }

                IntPtr nativeRegisterManipulatorsBridgeParams =
                    Marshal.AllocHGlobal(Marshal.SizeOf(registerManipulatorsBridgeParams));
                Marshal.StructureToPtr(registerManipulatorsBridgeParams, nativeRegisterManipulatorsBridgeParams, true);
                solverCommandBuffer.Clear();
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.RegisterManipulators,
                                                      nativeRegisterManipulatorsBridgeParams);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);

                gcparamBuffer2.Free();
            }
            else
            {
                Debug.LogWarning("No manipulator manipulatorManager has been set");
            }

            cameraRenderParams = new CameraParams();
            renderParams = new RenderParams();
            meshRenderGlobalParams = new MeshRenderGlobalParams();

            var registerParticlesBuffersParams = new RegisterParticlesBuffersBridgeParams();
            registerParticlesBuffersParams.PositionMass = GetNativePtr(PositionMass);
            registerParticlesBuffersParams.AffineVelocity0 = GetNativePtr(Affine[0]);
            registerParticlesBuffersParams.AffineVelocity1 = GetNativePtr(Affine[1]);
            registerParticlesBuffersParams.ParticleNumber = GetNativePtr(ParticleNumber);

            IntPtr nativeRegisterParticlesBuffersParams =
                Marshal.AllocHGlobal(Marshal.SizeOf(registerParticlesBuffersParams));
            Marshal.StructureToPtr(registerParticlesBuffersParams, nativeRegisterParticlesBuffersParams, true);
            solverCommandBuffer.Clear();
            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.RegisterParticlesBuffers,
                                                  nativeRegisterParticlesBuffersParams);
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            toFreeOnExit.Add(nativeRegisterParticlesBuffersParams);
        }

        public int GetParticleCountRounded()
        {
            return (int)Math.Ceiling((double)MaxNumParticles / MPM_THREADS) * MPM_THREADS; // round to workgroup size;
        }

        public ulong GetParticleCountFootprint()
        {
            ulong result = 0;
            int particleCountRounded = GetParticleCountRounded();
            result += (ulong)(MaxNumParticles * 4 * sizeof(float));            // PositionMass
            result += (ulong)(2 * 4 * particleCountRounded * 2 * sizeof(int)); // Affine
            result += (ulong)(particleCountRounded * 4 * sizeof(float));       // positionMassCopy
            result += (ulong)(particleCountRounded * 2 * sizeof(int));         // nodeParticlePairs

            result += (ulong)(4 * MaxNumParticles * sizeof(int)); // nodeParticlePairs0 nodeParticlePairs1
            int RadixWorkGroups1 = (int)Math.Ceiling((float)MaxNumParticles / (float)(2 * RADIX_THREADS));
            int RadixWorkGroups2 = (int)Math.Ceiling((float)MaxNumParticles / (float)(RADIX_THREADS * RADIX_THREADS));
            int RadixWorkGroups3 = (int)Math.Ceiling((float)RadixWorkGroups2 / (float)RADIX_THREADS);
            result += (ulong)(RadixWorkGroups1 * HISTO_WIDTH * sizeof(int));       // RadixGroupData1
            result += (ulong)(RadixWorkGroups2 * HISTO_WIDTH * sizeof(int));       // RadixGroupData2
            result += (ulong)((RadixWorkGroups3 + 1) * HISTO_WIDTH * sizeof(int)); // RadixGroupData3

            return result;
        }

        public ulong GetCollidersFootprint()
        {
            ulong result = 0;

            foreach (var collider in sdfColliders)
            {
                result += collider.GetMemoryFootrpint();
            }

            int ManipSize = Marshal.SizeOf(typeof(ZibraManipulatorManager.ManipulatorParam));

            result += (ulong)(manipulators.Count * ManipSize);   // DynamicManipData
            result += (ulong)(manipulators.Count * sizeof(int)); // ConstManipData

            return result;
        }

        public ulong GetGridFootprint()
        {
            ulong result = 0;

            GridSize = Vector3Int.CeilToInt(containerSize / CellSize);
            numNodes = GridSize[0] * GridSize[1] * GridSize[2];

            result += (ulong)(numNodes * 4 * sizeof(int));    // GridData
            result += (ulong)(numNodes * 4 * sizeof(float));  // GridNormal
            result += (ulong)(numNodes * sizeof(float));      // GridBlur0
            result += (ulong)(numNodes * sizeof(float));      // GridBlur1
            result += (ulong)(numNodes * sizeof(float));      // GridSDF
            result += (ulong)(numNodes * 2 * sizeof(int));    // IndexGrid
            result += (ulong)(numNodes * sizeof(int));        // VertexIDGrid
            result += (ulong)(numNodes * 4 * sizeof(float));  // VertexBuffer
            result += (ulong)(numNodes * sizeof(uint));       // QuadBuffer
            result += (ulong)(numNodes * (sizeof(uint) * 4)); // VertexProperties
            result += (ulong)(numNodes * 2 * sizeof(float));  // GridNormalTexture
            result += (ulong)(numNodes * sizeof(float) / 2);  // DensityTexture
            result += (ulong)(numNodes * sizeof(float) / 2);  // VelocityTexture

            return result;
        }

        void InitVolumeTexture(ref RenderTexture volume, GraphicsFormat format)
        {
            if (volume)
                return;
            volume = new RenderTexture(GridSize.x, GridSize.y, 0, format);
            volume.volumeDepth = GridSize.z;
            volume.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            volume.enableRandomWrite = true;
            volume.filterMode = FilterMode.Trilinear;
            volume.Create();
            if (!volume.IsCreated())
            {
                volume = null;
                throw new NotSupportedException("Failed to create 3D texture.");
            }
        }

        private void InitializeSolver()
        {
            simulationInternalTime = 0.0f;
            simulationInternalFrame = 0;
            numNodes = GridSize[0] * GridSize[1] * GridSize[2];
            GridData = new ComputeBuffer(numNodes * 4, sizeof(uint));
            GridNormal = new ComputeBuffer(numNodes, 4 * sizeof(float));
            GridBlur0 = new ComputeBuffer(numNodes, sizeof(float));
            GridBlur1 = new ComputeBuffer(numNodes, sizeof(float));
            GridSDF = new ComputeBuffer(numNodes, sizeof(float));

            ParticleSpeciesData = new ComputeBuffer(1, Marshal.SizeOf(typeof(ParticleSpeciesParameters)));

            // TODO: Test LDS atomics + global atomic vs AppendBuffer performance
            Counters = new ComputeBuffer(8, sizeof(uint));

            VertexIDGrid = new ComputeBuffer(numNodes, sizeof(int));
            VertexBuffer0 = CreateGraphicsBuffer(GraphicsBufferType.Vertex, 6 * numNodes, sizeof(uint));
            VertexBuffer1 = CreateGraphicsBuffer(GraphicsBufferType.Vertex, 4 * numNodes, sizeof(uint));

            TransferDataBuffer = new ComputeBuffer(1, sizeof(uint));
            MeshRenderIndexBuffer = CreateGraphicsBuffer(GraphicsBufferType.Index, 3 * numNodes, sizeof(uint));

            if (CurrentRenderingMode == RenderingMode.UnityRender)
            {
                LiquidMesh = new Mesh();

                var layout = new[] {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                };

                int maxVertexCount = numNodes;
                int maxTriangleCount =
                    (int)(maxVertexCount * renderingParameters.MaxLiquidMeshSize / 3.0f + ADDITIONAL_VERTICES);
                int indexBufferSize = maxTriangleCount * 3;
                int vertexBufferSize = maxTriangleCount * 2;

                LiquidMesh.SetVertexBufferParams(indexBufferSize, layout);
                LiquidMesh.SetIndexBufferParams(vertexBufferSize, IndexFormat.UInt32);
                LiquidMesh.MarkDynamic();
                LiquidMesh.SetVertices(new Vector3[vertexBufferSize], 0, vertexBufferSize);
                LiquidMesh.SetIndices(new int[indexBufferSize], MeshTopology.Triangles, 0);
                LiquidMesh.bounds = new Bounds(Vector3.zero, containerSize);
#if UNITY_2021_2_OR_NEWER
                LiquidMesh.vertexBufferTarget |= GraphicsBuffer.Target.CopyDestination;
                LiquidMesh.indexBufferTarget |= GraphicsBuffer.Target.CopyDestination;
#endif
            }

            // the max number of quads possible is about 3*numNodes, but in reality it should not be more than numNodes
            // in any case
            QuadBuffer = new ComputeBuffer(numNodes, sizeof(int));
            VertexProperties = CreateGraphicsBuffer(GraphicsBufferType.Vertex, numNodes, 6 * sizeof(uint));

            IndexGrid = new ComputeBuffer(numNodes, 2 * sizeof(int));

            InitVolumeTexture(ref GridNormalTexture,
                              SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.LoadStore)
                                  ? GraphicsFormat.R16G16B16A16_SFloat
                                  : GraphicsFormat.R32G32B32A32_SFloat);
            GridNormalTexture.name = "GridNormalTexture";
            InitVolumeTexture(ref DensityTexture,
                              SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.LoadStore)
                                  ? GraphicsFormat.R16G16B16A16_SFloat
                                  : GraphicsFormat.R32G32B32A32_SFloat);
            DensityTexture.name = "DensityTexture";
            InitVolumeTexture(ref VelocityTexture,
                              SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.LoadStore)
                                  ? GraphicsFormat.R16G16B16A16_SFloat
                                  : GraphicsFormat.R32G32B32A32_SFloat);
            DensityTexture.name = "VelocityTexture";

            int NumParticlesRounded = GetParticleCountRounded();

            positionMassCopy = new ComputeBuffer(NumParticlesRounded, 4 * sizeof(float));
            nodeParticlePairs0 = new ComputeBuffer(2 * NumParticlesRounded, sizeof(int));
            nodeParticlePairs1 = new ComputeBuffer(2 * NumParticlesRounded, sizeof(int));
            int RadixWorkGroups1 = (int)Math.Ceiling((float)MaxNumParticles / (float)(2 * RADIX_THREADS));
            int RadixWorkGroups2 = (int)Math.Ceiling((float)MaxNumParticles / (float)(RADIX_THREADS * RADIX_THREADS));
            int RadixWorkGroups3 = (int)Math.Ceiling((float)RadixWorkGroups2 / (float)RADIX_THREADS);
            RadixGroupData1 = new ComputeBuffer(RadixWorkGroups1 * HISTO_WIDTH, sizeof(uint));
            RadixGroupData2 = new ComputeBuffer(RadixWorkGroups2 * HISTO_WIDTH, sizeof(uint));
            RadixGroupData3 = new ComputeBuffer((RadixWorkGroups3 + 1) * HISTO_WIDTH, sizeof(uint));

#if ZIBRA_LIQUID_DEBUG
            GridData.name = "GridData";
            GridNormal.name = "GridNormal";
            GridBlur0.name = "GridBlur0";
            GridBlur1.name = "GridBlur1";
            GridSDF.name = "GridSDF";
            IndexGrid.name = "IndexGrid";
            positionMassCopy.name = "positionMassCopy";
            nodeParticlePairs0.name = "NodeParticlePairs0";
            nodeParticlePairs1.name = "NodeParticlePairs1";
            RadixGroupData1.name = "RadixGroupData1";
            RadixGroupData2.name = "RadixGroupData2";
            RadixGroupData3.name = "RadixGroupData3";
            ParticleSpeciesData.name = "ParticleSpeciesData";
#endif

            SetFluidParameters();

            var gcparamBuffer = GCHandle.Alloc(fluidParameters, GCHandleType.Pinned);

            var registerSolverBuffersBridgeParams = new RegisterSolverBuffersBridgeParams();
            registerSolverBuffersBridgeParams.SimulationParams = gcparamBuffer.AddrOfPinnedObject();

            registerSolverBuffersBridgeParams.ParticleSpeciesCount = 1;
            registerSolverBuffersBridgeParams.PositionMassCopy = GetNativePtr(positionMassCopy);
            registerSolverBuffersBridgeParams.GridData = GetNativePtr(GridData);
            registerSolverBuffersBridgeParams.IndexGrid = GetNativePtr(IndexGrid);
            registerSolverBuffersBridgeParams.GridBlur0 = GetNativePtr(GridBlur0);
            registerSolverBuffersBridgeParams.GridBlur1 = GetNativePtr(GridBlur1);
            registerSolverBuffersBridgeParams.GridNormal = GetNativePtr(GridNormal);
            registerSolverBuffersBridgeParams.GridSDF = GetNativePtr(GridSDF);
            registerSolverBuffersBridgeParams.NodeParticlePairs0 = GetNativePtr(nodeParticlePairs0);
            registerSolverBuffersBridgeParams.NodeParticlePairs1 = GetNativePtr(nodeParticlePairs1);
            registerSolverBuffersBridgeParams.RadixGroupData1 = GetNativePtr(RadixGroupData1);
            registerSolverBuffersBridgeParams.RadixGroupData2 = GetNativePtr(RadixGroupData2);
            registerSolverBuffersBridgeParams.RadixGroupData3 = GetNativePtr(RadixGroupData3);
            registerSolverBuffersBridgeParams.Counters = GetNativePtr(Counters);
            registerSolverBuffersBridgeParams.VertexIDGrid = GetNativePtr(VertexIDGrid);
            registerSolverBuffersBridgeParams.VertexBuffer0 = GetNativePtr(VertexBuffer0);
            registerSolverBuffersBridgeParams.VertexBuffer1 = GetNativePtr(VertexBuffer1);
            registerSolverBuffersBridgeParams.QuadBuffer = GetNativePtr(QuadBuffer);
            registerSolverBuffersBridgeParams.GridDensity = MakeTextureNativeBridge(DensityTexture);
            registerSolverBuffersBridgeParams.GridVelocity = MakeTextureNativeBridge(VelocityTexture);
            registerSolverBuffersBridgeParams.GridNormals = MakeTextureNativeBridge(GridNormalTexture);
            if (CurrentRenderingMode == RenderingMode.UnityRender)
            {
                registerSolverBuffersBridgeParams.UnityMeshVertexBuffer = LiquidMesh.GetNativeVertexBufferPtr(0);
                registerSolverBuffersBridgeParams.UnityMeshIndexBuffer = LiquidMesh.GetNativeIndexBufferPtr();
            }
            else
            {
                registerSolverBuffersBridgeParams.UnityMeshVertexBuffer = IntPtr.Zero;
                registerSolverBuffersBridgeParams.UnityMeshIndexBuffer = IntPtr.Zero;
            }
            registerSolverBuffersBridgeParams.TransferDataBuffer = GetNativePtr(TransferDataBuffer);
            registerSolverBuffersBridgeParams.MeshRenderIndexBuffer = GetNativePtr(MeshRenderIndexBuffer);
            registerSolverBuffersBridgeParams.VertexData = GetNativePtr(VertexProperties);
            registerSolverBuffersBridgeParams.ParticleSpeciesData = GetNativePtr(ParticleSpeciesData);
            IntPtr nativeRegisterSolverBuffersBridgeParams =
                Marshal.AllocHGlobal(Marshal.SizeOf(registerSolverBuffersBridgeParams));
            Marshal.StructureToPtr(registerSolverBuffersBridgeParams, nativeRegisterSolverBuffersBridgeParams, true);
            solverCommandBuffer.Clear();
            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.RegisterSolverBuffers,
                                                  nativeRegisterSolverBuffersBridgeParams);
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            gcparamBuffer.Free();
            solverCommandBuffer.Clear();

            toFreeOnExit.Add(nativeRegisterSolverBuffersBridgeParams);
        }

        /// <summary>
        /// Initializes a new instance of ZibraFluid
        /// </summary>
        public void Init()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            try
            {
#if UNITY_PIPELINE_HDRP
                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
                    bool missingRequiredParameter = false;

                    if (customLightHDRP == null && CurrentRenderingMode != RenderingMode.UnityRender)
                    {
                        Debug.LogError("No Custom Light set in Zibra Liquid.");
                        missingRequiredParameter = true;
                    }

                    if (reflectionProbeHDRP == null && CurrentRenderingMode != RenderingMode.UnityRender)
                    {
                        Debug.LogError("No reflection probe added to Zibra Liquid.");
                        missingRequiredParameter = true;
                    }

                    if (missingRequiredParameter)
                    {
                        throw new Exception("Liquid creation failed due to missing parameter.");
                    }
                }
#endif

#if ZIBRA_LIQUID_PAID_VERSION
                if (InitialState == ZibraLiquid.InitialStateType.NoParticles || BakedInitialStateAsset == null)
#endif
                {
                    bool haveEmitter = false;
                    foreach (var manipulator in manipulators)
                    {
                        if (manipulator.GetManipulatorType() == Manipulator.ManipulatorType.Emitter)
                        {
                            haveEmitter = true;
                            break;
                        }
                    }

                    if (!haveEmitter)
                    {
#if ZIBRA_LIQUID_PAID_VERSION
                        throw new Exception("Liquid creation failed. Liquid have neither initial state nor emitters.");
#else
                        throw new Exception("Liquid creation failed. Liquid have don't have any emitters.");
#endif
                    }
                }

                Camera.onPreRender += RenderCallBackWrapper;

                solverCommandBuffer = new CommandBuffer { name = "ZibraLiquid.Solver" };

                CurrentInstanceID = ms_NextInstanceId++;

                ForceCloseCommandEncoder(solverCommandBuffer);
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.CreateFluidInstance);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);
                solverCommandBuffer.Clear();

                InitializeParticles();

                var initializeGPUReadbackParamsBridgeParams = new InitializeGPUReadbackParams();
#if ZIBRA_LIQUID_FREE_VERSION
                UInt32 manipSize = 0;
#else
                UInt32 manipSize = (UInt32)manipulatorManager.Elements * STATISTICS_PER_MANIPULATOR * sizeof(Int32);
#endif
                initializeGPUReadbackParamsBridgeParams.readbackBufferSize = sizeof(Int32) + manipSize;
                switch (SystemInfo.graphicsDeviceType)
                {
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.XboxOne:
                case GraphicsDeviceType.Switch:
#if UNITY_2020_3_OR_NEWER
                case GraphicsDeviceType.Direct3D12:
                case GraphicsDeviceType.XboxOneD3D12:
#endif
                    initializeGPUReadbackParamsBridgeParams.maxFramesInFlight = QualitySettings.maxQueuedFrames + 1;
                    break;
                default:
                    initializeGPUReadbackParamsBridgeParams.maxFramesInFlight = (int)this.maxFramesInFlight;
                    break;
                }

                IntPtr nativeCreateInstanceBridgeParams =
                    Marshal.AllocHGlobal(Marshal.SizeOf(initializeGPUReadbackParamsBridgeParams));
                Marshal.StructureToPtr(initializeGPUReadbackParamsBridgeParams, nativeCreateInstanceBridgeParams, true);

                solverCommandBuffer.Clear();
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.InitializeGpuReadback,
                                                      nativeCreateInstanceBridgeParams);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);

                toFreeOnExit.Add(nativeCreateInstanceBridgeParams);

                InitializeSolver();

                initialized = true;
                // hack to make editor -> play mode transition work when the liquid is initialized
                forceTextureUpdate = true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                ClearRendering();
                ClearSolver();

                initialized = false;
            }
        }

        protected void Update()
        {
            if (!initialized)
            {
                return;
            }

            ZibraLiquidGPUGarbageCollector.GCUpdateWrapper();

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return;
            }
#endif

            if (!useFixedTimestep)
                UpdateSimulation(Time.smoothDeltaTime);

            UpdateReadback();
        }

        protected void FixedUpdate()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                return;
            }
#endif

            if (useFixedTimestep)
                UpdateSimulation(Time.fixedDeltaTime);
        }

        public void UpdateReadback()
        {
            if (!IsSimulationEnabled())
            {
                return;
            }

            solverCommandBuffer.Clear();

            // This must be called at most ONCE PER FRAME
            // Otherwise you'll get deadlock
            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.UpdateReadback);

            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            /// ParticleNumber GPUReadback
            UInt32 size = sizeof(UInt32);
            IntPtr readbackData = ZibraLiquidBridge.GPUReadbackGetData(CurrentInstanceID, size);
            if (readbackData != IntPtr.Zero)
            {
                activeParticleNumber = Marshal.ReadInt32(readbackData);
            }

            UpdateManipulatorStatistics();
        }

        public void UpdateUnityRender()
        {
            if (CurrentRenderingMode == RenderingMode.UnityRender)
            {
                Transform meshTransform = transform.Find("ZibraLiquidMesh");

                if (meshTransform == null)
                {
                    GameObject obj = new GameObject();
                    obj.name = "ZibraLiquidMesh";
                    meshTransform = obj.transform;
                    meshTransform.SetParent(transform, false);
                }

                GameObject meshObject = meshTransform.gameObject;

                // Add renderer components if not present
                if (meshObject.GetComponent<MeshFilter>() == null)
                {
                    meshObject.AddComponent(typeof(MeshFilter));
                }

                if (meshObject.GetComponent<MeshRenderer>() == null)
                {
                    meshObject.AddComponent(typeof(MeshRenderer));
                    MeshRenderer meshRenderer = meshObject.GetComponent<MeshRenderer>();
                    // TODO make a custom liquid material
                    meshRenderer.material = new Material(Shader.Find("Diffuse"));
                    meshRenderer.enabled = true;
                }
                else
                {
                    MeshRenderer meshRenderer = meshObject.GetComponent<MeshRenderer>();
                    meshRenderer.enabled = true;
                }

                MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
                if (meshFilter.sharedMesh != LiquidMesh)
                {
                    meshFilter.sharedMesh = LiquidMesh;
                }

                meshObject.SetActive(runRendering);
            }
            else
            {
                Transform meshTransform = transform.Find("ZibraLiquidMesh");

                if (meshTransform == null)
                    return;

                GameObject meshObject = meshTransform.gameObject;
                meshObject.SetActive(false);
            }
        }

        public void UpdateSimulation(float deltaTime)
        {
            UpdateUnityRender();
            UpdateNativeRenderParams();

            if (!IsSimulationEnabled())
            {
                return;
            }

            timestep = Math.Min(simTimePerSec * deltaTime / (float)iterationsPerFrame, timeStepMax);

            for (var i = 0; i < iterationsPerFrame; i++)
            {
                StepPhysics();
            }

            solverCommandBuffer.Clear();
            // copy grid data to 3d texture for rendering after physics steps
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

#if UNITY_EDITOR
            NotifyChange();
#endif

            particleMass = 1.0f;
        }

        /// <summary>
        /// Update the material parameters
        /// </summary>
        public bool SetMaterialParams(Camera cam)
        {
            bool isDirty = false;

            CameraResources camRes = cameraResources[cam];
            Material usedUpscaleMaterial = EnableDownscale ? materialParameters.UpscaleMaterial : null;

            isDirty = camRes.upscaleMaterial.SetMaterial(usedUpscaleMaterial) || isDirty;

            bool usingMainMaterial = ActiveRenderingMode == RenderingMode.MeshRender;

            Material CurrentSharedMaterial = usingMainMaterial ? materialParameters.FluidMeshMaterial : null;
            isDirty = camRes.liquidMaterial.SetMaterial(CurrentSharedMaterial) || isDirty;
            Material CurrentMaterial = camRes.liquidMaterial.currentMaterial;

            if (usingMainMaterial)
            {
                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
#if UNITY_PIPELINE_HDRP
                    if (customLightHDRP == null)
                        Debug.LogError("No Custom Light set in Zibra Liquid.");
                    else
                        CurrentMaterial.SetVector("WorldSpaceLightPos", customLightHDRP.transform.position);

                    if (reflectionProbeHDRP == null)
                        Debug.LogError("No reflection probe added to Zibra Liquid.");
#endif // UNITY_PIPELINE_HDRP
                }
                else
                {
                    if (reflectionProbeSRP != null) // custom reflection probe
                    {
                        usingCustomReflectionProbe = true;
                        CurrentMaterial.SetTexture("ReflectionProbe", reflectionProbeSRP.texture);
                        CurrentMaterial.SetVector("ReflectionProbe_HDR", reflectionProbeSRP.textureHDRDecodeValues);
                        CurrentMaterial.SetVector("ReflectionProbe_BoxMax", reflectionProbeSRP.bounds.max);
                        CurrentMaterial.SetVector("ReflectionProbe_BoxMin", reflectionProbeSRP.bounds.min);
                        CurrentMaterial.SetVector("ReflectionProbe_ProbePosition",
                                                  reflectionProbeSRP.transform.position);
                    }
                    else
                    {
                        usingCustomReflectionProbe = false;
                    }
                }

                CurrentMaterial.SetFloat("AbsorptionAmount", materialParameters.AbsorptionAmount);
                CurrentMaterial.SetFloat("ScatteringAmount", materialParameters.ScatteringAmount);
                CurrentMaterial.SetFloat("Metalness", materialParameters.Metalness);
                CurrentMaterial.SetFloat("RefractionDistortion", materialParameters.IndexOfRefraction - 1.0f);
                CurrentMaterial.SetFloat("LiquidIOR", materialParameters.IndexOfRefraction);
                CurrentMaterial.SetFloat("Roughness", materialParameters.Roughness);
                CurrentMaterial.SetVector("RefractionColor", materialParameters.Color);
                CurrentMaterial.SetVector("ReflectionColor", materialParameters.ReflectionColor);
                CurrentMaterial.SetVector("EmissiveColor", materialParameters.EmissiveColor);

#if UNITY_PIPELINE_HDRP
                CurrentMaterial.SetVector("LightColor",
                                          customLightHDRP.color * Mathf.Log(customLightHDRP.intensity) / 8.0f);
                CurrentMaterial.SetVector("LightDirection", customLightHDRP.transform.rotation * new Vector3(0, 0, -1));
#endif

                CurrentMaterial.SetVector("ContainerScale", containerSize);
                CurrentMaterial.SetVector("ContainerPosition", containerPos);
                CurrentMaterial.SetVector("GridSize", (Vector3)GridSize);
                CurrentMaterial.SetFloat("RayMarchResolutionDownscale",
                                         renderingParameters.RayMarchingResolutionDownscale);
                CurrentMaterial.SetFloat("ParticleDiameter", particleDiameter);
                CurrentMaterial.SetFloat("RefractionMinimumDepth", 1e-4f);
                CurrentMaterial.SetFloat("RefractionDepthBias", 1.25f);

                CurrentMaterial.SetTexture("GridNormals", GridNormalTexture);
                CurrentMaterial.SetTexture("MeshRenderData", color0);
                CurrentMaterial.SetTexture("RayMarchData", color1);
                CurrentMaterial.SetTexture("MaterialData", color2);
                CurrentMaterial.SetTexture("MeshDepth", depth);
                CurrentMaterial.SetTexture("GridDensity", DensityTexture);

                if (renderingParameters.RefractionBounces ==
                    ZibraLiquidAdvancedRenderParameters.RayMarchingBounces.TwoBounces)
                {
                    if (meshRenderGlobalParams.TwoBouncesEnabled == 0)
                        isDirty = true;
                    meshRenderGlobalParams.TwoBouncesEnabled = 1;
                }
                else
                {
                    if (meshRenderGlobalParams.TwoBouncesEnabled == 1)
                        isDirty = true;
                    meshRenderGlobalParams.TwoBouncesEnabled = 0;
                }

#if UNITY_IOS && !UNITY_EDITOR
                CurrentMaterial.EnableKeyword("FLIP_BACKGROUND");
                if (!EnableDownscale)
                {
                    CurrentMaterial.EnableKeyword("FLIP_NATIVE_TEXTURES");
                }
                else
                {
                    CurrentMaterial.DisableKeyword("FLIP_NATIVE_TEXTURES");
                }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan && !EnableDownscale)
                {
                    CurrentMaterial.EnableKeyword("FLIP_BACKGROUND");
                    CurrentMaterial.EnableKeyword("FLIP_NATIVE_TEXTURES");
                }
#endif
                CurrentMaterial.SetTexture("Background", GetBackgroundToBind(cam));
                CurrentMaterial.SetTexture("FluidColor", color0);

                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
#if UNITY_PIPELINE_HDRP
                    CurrentMaterial.SetTexture("ReflectionProbe", reflectionProbeHDRP.texture);
                    CurrentMaterial.SetVector("ReflectionProbe_HDR", new Vector4(0.01f, 1.0f));
                    CurrentMaterial.SetVector("ReflectionProbe_BoxMax", reflectionProbeHDRP.bounds.max);
                    CurrentMaterial.SetVector("ReflectionProbe_BoxMin", reflectionProbeHDRP.bounds.min);
                    CurrentMaterial.SetVector("ReflectionProbe_ProbePosition", reflectionProbeHDRP.transform.position);
                    CurrentMaterial.EnableKeyword("HDRP");
#endif
                }
                else
                {
                    if (usingCustomReflectionProbe)
                    {
                        CurrentMaterial.EnableKeyword("CUSTOM_REFLECTION_PROBE");
                    }
                    else
                    {
                        CurrentMaterial.DisableKeyword("CUSTOM_REFLECTION_PROBE");
                    }
                }
            }

            Material usedSDFRenderMaterial = visualizeSceneSDF ? materialParameters.SDFRenderMaterial : null;
            isDirty = camRes.sdfRenderMaterial.SetMaterial(usedSDFRenderMaterial) || isDirty;

            if (visualizeSceneSDF)
            {
                Material CurrentSDFRenderMaterial = camRes.sdfRenderMaterial.currentMaterial;
                CurrentSDFRenderMaterial.SetTexture("SDFRender", color0);

                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
#if UNITY_PIPELINE_HDRP
                    CurrentSDFRenderMaterial.SetVector("LightColor", customLightHDRP.color *
                                                                         Mathf.Log(customLightHDRP.intensity) / 8.0f);
                    CurrentSDFRenderMaterial.SetVector("LightDirection",
                                                       customLightHDRP.transform.rotation * new Vector3(0, 0, -1));
                    CurrentSDFRenderMaterial.EnableKeyword("HDRP");
#endif
                }
            }

            return isDirty;
        }

        public Vector2Int ApplyDownscaleFactor(Vector2Int val)
        {
            if (!EnableDownscale)
                return val;
            return new Vector2Int((int)(val.x * DownscaleFactor), (int)(val.y * DownscaleFactor));
        }

        public Vector2Int ApplyRenderPipelineRenderScale(Vector2Int val, float renderPipelineRenderScale)
        {
            return new Vector2Int((int)(val.x * renderPipelineRenderScale), (int)(val.y * renderPipelineRenderScale));
        }

        private bool CreateTexture(ref RenderTexture texture, Vector2Int resolution, bool applyDownscaleFactor,
                                   FilterMode filterMode, int depth, RenderTextureFormat format,
                                   bool enableRandomWrite = false)
        {
            if (texture == null || texture.width != resolution.x || texture.height != resolution.y ||
                forceTextureUpdate)
            {
                ZibraLiquidGPUGarbageCollector.SafeRelease(texture);
                texture = null;
                texture = new RenderTexture(resolution.x, resolution.y, depth, format);
                texture.enableRandomWrite = enableRandomWrite;
                texture.filterMode = filterMode;
                texture.Create();
                return true;
            }

            return false;
        }

        // Returns resolution that is enough for all cameras
        private Vector2Int GetRequiredTextureResolution()
        {
            if (camRenderResolutions.Count == 0)
                Debug.Log("camRenderResolutions dictionary was empty when GetRequiredTextureResolution was called.");

            Vector2Int result = new Vector2Int(0, 0);
            foreach (var item in camRenderResolutions)
            {
                result = Vector2Int.Max(result, item.Value);
            }

            return result;
        }

        public bool IsBackgroundCopyNeeded(Camera cam)
        {
            return !EnableDownscale || (cam.activeTexture == null);
        }

        private RenderTexture GetBackgroundToBind(Camera cam)
        {
            if (!IsBackgroundCopyNeeded(cam))
                return cam.activeTexture;
            return cameraResources[cam].background;
        }

        /// <summary>
        /// Removes disabled/inactive cameras from cameraResources
        /// </summary>
        private void UpdateCameraList()
        {
            List<Camera> toRemove = new List<Camera>();
            foreach (var camResource in cameraResources)
            {
                if (camResource.Key == null ||
                    (!camResource.Key.isActiveAndEnabled && camResource.Key.cameraType != CameraType.SceneView))
                {
                    toRemove.Add(camResource.Key);
                    continue;
                }
            }

            foreach (var cam in toRemove)
            {
                if (cameraResources[cam].background)
                {
                    cameraResources[cam].background.Release();
                    cameraResources[cam].background = null;
                }

                cameraResources.Remove(cam);
            }
        }

        void UpdateCameraResolution(Camera cam, float renderPipelineRenderScale)
        {
            Vector2Int cameraResolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            cameraResolution = ApplyRenderPipelineRenderScale(cameraResolution, renderPipelineRenderScale);
            camNativeResolutions[cam] = cameraResolution;
            Vector2Int cameraResolutionDownscaled = ApplyDownscaleFactor(cameraResolution);
            camRenderResolutions[cam] = cameraResolutionDownscaled;
        }

        /// <summary>
        /// Update Native textures for a given camera
        /// </summary>
        /// <param name="cam">Camera</param>
        public bool UpdateNativeTextures(Camera cam, float renderPipelineRenderScale)
        {
            UpdateCameraList();

            Vector2Int cameraResolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            cameraResolution = ApplyRenderPipelineRenderScale(cameraResolution, renderPipelineRenderScale);

            Vector2Int textureResolution = GetRequiredTextureResolution();
            int pixelCount = textureResolution.x * textureResolution.y;

            if (!cameras.Contains(cam))
            {
                // add camera to list
                cameras.Add(cam);
            }

            int CameraID = cameras.IndexOf(cam);

            bool isGlobalTexturesDirty = false;
            bool isCameraDirty = cameraResources[cam].isDirty;

            FilterMode defaultFilter = EnableDownscale ? FilterMode.Bilinear : FilterMode.Point;

            if (IsBackgroundCopyNeeded(cam))
            {
                if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
                {
#if UNITY_PIPELINE_HDRP
                    isCameraDirty = CreateTexture(ref cameraResources[cam].background, cameraResolution, false,
                                                  FilterMode.Point, 0, RenderTextureFormat.ARGBHalf) ||
                                    isCameraDirty;
#endif
                }
                else
                {
                    var format =
                        SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.LoadStore)
                            ? RenderTextureFormat.RGB111110Float
                            : RenderTextureFormat.ARGB32; // 8 bits per component
                    isCameraDirty = CreateTexture(ref cameraResources[cam].background, cameraResolution, false,
                                                  FilterMode.Point, 0, format) ||
                                    isCameraDirty;
                }
            }
            else
            {
                if (cameraResources[cam].background != null)
                {
                    isCameraDirty = true;
                    cameraResources[cam].background.Release();
                    cameraResources[cam].background = null;
                }
            }

            isGlobalTexturesDirty =
                CreateTexture(ref depth, textureResolution, true, defaultFilter, 32, RenderTextureFormat.Depth) ||
                isGlobalTexturesDirty;
            isGlobalTexturesDirty = CreateTexture(ref color0, textureResolution, true, FilterMode.Point, 0,
                                                  RenderTextureFormat.ARGBFloat, true) ||
                                    isGlobalTexturesDirty;
            // TODO skip creation if not used
            isGlobalTexturesDirty = CreateTexture(ref color1, textureResolution, true, FilterMode.Point, 0,
                                                  RenderTextureFormat.ARGBFloat, true) ||
                                    isGlobalTexturesDirty;
            isGlobalTexturesDirty = CreateTexture(ref color2, textureResolution, true, FilterMode.Point, 0,
                                                  RenderTextureFormat.ARGBFloat, true) ||
                                    isGlobalTexturesDirty;
            isGlobalTexturesDirty = CreateTexture(ref upscaleColor, textureResolution, true, FilterMode.Point, 0,
                                                  RenderTextureFormat.ARGBHalf, true) ||
                                    isGlobalTexturesDirty;

            if (isGlobalTexturesDirty || isCameraDirty || forceTextureUpdate)
            {
                if (isGlobalTexturesDirty || forceTextureUpdate)
                {
                    foreach (var camera in cameraResources)
                    {
                        camera.Value.isDirty = true;
                    }

                    CurrentTextureResolution = textureResolution;
                }

                cameraResources[cam].isDirty = false;

                var registerRenderResourcesBridgeParams = new RegisterRenderResourcesBridgeParams();
                registerRenderResourcesBridgeParams.Depth = MakeTextureNativeBridge(depth);
                registerRenderResourcesBridgeParams.Color0 = MakeTextureNativeBridge(color0);
                registerRenderResourcesBridgeParams.Color1 = MakeTextureNativeBridge(color1);
                registerRenderResourcesBridgeParams.Color2 = MakeTextureNativeBridge(color2);

                IntPtr nativeRegisterRenderResourcesBridgeParams =
                    Marshal.AllocHGlobal(Marshal.SizeOf(registerRenderResourcesBridgeParams));
                Marshal.StructureToPtr(registerRenderResourcesBridgeParams, nativeRegisterRenderResourcesBridgeParams,
                                       true);
                solverCommandBuffer.Clear();
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.RegisterRenderResources,
                                                      nativeRegisterRenderResourcesBridgeParams);

                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.InitializeGraphicsPipeline);

                Graphics.ExecuteCommandBuffer(solverCommandBuffer);

                toFreeOnExit.Add(nativeRegisterRenderResourcesBridgeParams);

                forceTextureUpdate = false;
            }

            return isGlobalTexturesDirty || isCameraDirty;
        }

        /// <summary>
        /// Render the liquid from the native plugin
        /// </summary>
        /// <param name="cmdBuffer">Command Buffer to add the rendering commands to</param>
        public void RenderLiquidNative(CommandBuffer cmdBuffer, Camera cam, Rect? viewport = null)
        {
            ForceCloseCommandEncoder(cmdBuffer);

            ZibraLiquidBridge.SubmitInstanceEvent(cmdBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.SetCameraParams, camNativeParams[cam]);

            ZibraLiquidBridge.SubmitInstanceEvent(cmdBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.UpdateMeshRenderGlobalParameters,
                                                  camMeshRenderParams[cam]);

            ZibraLiquidBridge.SubmitInstanceEvent(cmdBuffer, CurrentInstanceID, ZibraLiquidBridge.EventID.Draw);
        }

        public void RenderLiquidMain(CommandBuffer cmdBuffer, Camera cam, Rect? viewport = null)
        {
            switch (ActiveRenderingMode)
            {
            case RenderingMode.MeshRender:
                RenderLiquidMesh(cmdBuffer, cam, viewport);
                break;
            case RenderingMode.UnityRender:
                break;
            default:
                Debug.LogError("Unknown Rendering mode");
                break;
            }

            if (visualizeSceneSDF)
            {
                RenderSDFVisualization(cmdBuffer, cam, viewport);
            }
        }

        /// <summary>
        /// Upscale the liquid surface to currently bound render target
        /// Used for URP where we can't change render targets
        /// Used for URP where we can't change render targets
        /// </summary>
        public void UpscaleLiquidDirect(CommandBuffer cmdBuffer, Camera cam,
                                        RenderTargetIdentifier? sourceColorTexture = null,
                                        RenderTargetIdentifier? sourceDepthTexture = null, Rect? viewport = null)
        {
            Material CurrentUpscaleMaterial = cameraResources[cam].upscaleMaterial.currentMaterial;
            Vector2Int cameraNativeResolution = camNativeResolutions[cam];

            cmdBuffer.SetViewport(new Rect(0, 0, cameraNativeResolution.x, cameraNativeResolution.y));
            if (sourceColorTexture == null)
            {
                cmdBuffer.SetGlobalTexture("ShadedLiquid", upscaleColor);
            }
            else
            {
                cmdBuffer.SetGlobalTexture("ShadedLiquid", sourceColorTexture.Value);
            }

            cmdBuffer.DrawProcedural(transform.localToWorldMatrix, CurrentUpscaleMaterial, 0, MeshTopology.Triangles,
                                     6);
        }

        /// <summary>
        /// Render the liquid surface
        /// Camera's targetTexture must be copied to cameraResources[cam].background
        /// using corresponding Render Pipeline before calling this method
        /// </summary>
        /// <param name="cmdBuffer">Command Buffer to add the rendering commands to</param>
        /// <param name="cam">Camera</param>
        public void RenderFluid(CommandBuffer cmdBuffer, Camera cam, RenderTargetIdentifier? renderTargetParam = null,
                                RenderTargetIdentifier? depthTargetParam = null, Rect? viewport = null)
        {
            RenderTargetIdentifier renderTarget =
                renderTargetParam ?? new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);

            // Render fluid to temporary RenderTexture if downscale enabled
            // Otherwise render straight to final RenderTexture
            if (EnableDownscale)
            {
                cmdBuffer.SetRenderTarget(upscaleColor);
                cmdBuffer.ClearRenderTarget(true, true, Color.clear);
            }
            else
            {
                if (depthTargetParam != null)
                {
                    RenderTargetIdentifier depthTarget = depthTargetParam.Value;
                    cmdBuffer.SetRenderTarget(renderTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                                              depthTarget, RenderBufferLoadAction.Load,
                                              RenderBufferStoreAction.DontCare);
                }
                else
                {
                    cmdBuffer.SetRenderTarget(renderTarget);
                }
            }

            RenderLiquidMain(cmdBuffer, cam, viewport);

            // If downscale enabled then we need to blend it on top of final RenderTexture
            if (EnableDownscale)
            {
                if (depthTargetParam != null)
                {
                    RenderTargetIdentifier depthTarget = depthTargetParam.Value;
                    cmdBuffer.SetRenderTarget(renderTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                                              depthTarget, RenderBufferLoadAction.Load,
                                              RenderBufferStoreAction.DontCare);
                }
                else
                {
                    cmdBuffer.SetRenderTarget(renderTarget);
                }
                // depth in upscale
                UpscaleLiquidDirect(cmdBuffer, cam, null, null, viewport);
            }
        }

        /// <summary>
        /// Render the liquid surface
        /// Camera's targetTexture must be copied to cameraResources[cam].background
        /// using corresponding Render Pipeline before calling this method
        /// </summary>
        /// <param name="cmdBuffer">Command Buffer to add the rendering commands to</param>
        /// <param name="cam">Camera</param>
        public void RenderLiquidMesh(CommandBuffer cmdBuffer, Camera cam, Rect? viewport = null)
        {
            Vector2Int cameraRenderResolution = camRenderResolutions[cam];

            Material CurrentMaterial = cameraResources[cam].liquidMaterial.currentMaterial;

            // Render fluid to temporary RenderTexture if downscale enabled
            // Otherwise render straight to final RenderTexture
            if (EnableDownscale)
            {
                cmdBuffer.SetViewport(new Rect(0, 0, cameraRenderResolution.x, cameraRenderResolution.y));
            }
            else
            {
                if (viewport != null)
                {
                    cmdBuffer.SetViewport(viewport.Value);
                }
            }

            cmdBuffer.SetGlobalTexture("Background", GetBackgroundToBind(cam));
            if (RenderPipelineDetector.GetRenderPipelineType() == RenderPipelineDetector.RenderPipeline.HDRP)
            {
#if UNITY_PIPELINE_HDRP
                cmdBuffer.SetGlobalTexture("ReflectionProbe", reflectionProbeHDRP.texture);
                cmdBuffer.SetGlobalVector("ReflectionProbe_HDR", new Vector4(0.01f, 1.0f));
                cmdBuffer.SetGlobalVector("ReflectionProbe_BoxMax", reflectionProbeHDRP.bounds.max);
                cmdBuffer.SetGlobalVector("ReflectionProbe_BoxMin", reflectionProbeHDRP.bounds.min);
                cmdBuffer.SetGlobalVector("ReflectionProbe_ProbePosition", reflectionProbeHDRP.transform.position);
                CurrentMaterial.EnableKeyword("HDRP");
#endif
            }
            else
            {
                if (usingCustomReflectionProbe)
                {
                    CurrentMaterial.EnableKeyword("CUSTOM_REFLECTION_PROBE");
                }
                else
                {
                    CurrentMaterial.DisableKeyword("CUSTOM_REFLECTION_PROBE");
                }
            }

            cmdBuffer.DrawProcedural(transform.localToWorldMatrix, CurrentMaterial, 0, MeshTopology.Triangles, 6);
        }
        public void RenderSDFVisualization(CommandBuffer cmdBuffer, Camera cam, Rect? viewport = null)
        {
            ZibraLiquidBridge.SubmitInstanceEvent(cmdBuffer, CurrentInstanceID, ZibraLiquidBridge.EventID.RenderSDF);

            Vector2Int cameraRenderResolution = camRenderResolutions[cam];

            Material CurrentMaterial = cameraResources[cam].sdfRenderMaterial.currentMaterial;

            // Render fluid to temporary RenderTexture if downscale enabled
            // Otherwise render straight to final RenderTexture
            if (EnableDownscale)
            {
                cmdBuffer.SetViewport(new Rect(0, 0, cameraRenderResolution.x, cameraRenderResolution.y));
            }
            else
            {
                if (viewport != null)
                {
                    cmdBuffer.SetViewport(viewport.Value);
                }
            }

            cmdBuffer.DrawProcedural(transform.localToWorldMatrix, CurrentMaterial, 0, MeshTopology.Triangles, 6);
        }

        /// <summary>
        /// Update the camera parameters for the particle renderer
        /// </summary>
        /// <param name="cam">Camera</param>
        ///
        public void UpdateCamera(Camera cam)
        {
            Vector2Int resolution = camRenderResolutions[cam];

            Material CurrentMaterial = cameraResources[cam].liquidMaterial.currentMaterial;
            Material CurrentUpscaleMaterial = cameraResources[cam].upscaleMaterial.currentMaterial;
            Material CurrentSDFRenderMaterial = cameraResources[cam].sdfRenderMaterial.currentMaterial;

            Matrix4x4 Projection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            Matrix4x4 ProjectionInverse = Projection.inverse;
            Matrix4x4 View = cam.worldToCameraMatrix;
            Matrix4x4 ViewProjection = Projection * View;
            Matrix4x4 ViewProjectionInverse = ViewProjection.inverse;

            cameraRenderParams.View = cam.worldToCameraMatrix;
            cameraRenderParams.Projection = Projection;
            cameraRenderParams.ProjectionInverse = ProjectionInverse;
            cameraRenderParams.ViewProjection = ViewProjection;
            cameraRenderParams.ViewProjectionInverse = ViewProjectionInverse;
            cameraRenderParams.EyeRayCameraCoeficients = CalculateEyeRayCameraCoeficients(cam);
            cameraRenderParams.WorldSpaceCameraPos = cam.transform.position;
            cameraRenderParams.CameraResolution = new Vector2(resolution.x, resolution.y);
            cameraRenderParams.CameraID = cameras.IndexOf(cam);

            meshRenderGlobalParams.LiquidIOR = materialParameters.IndexOfRefraction;
            meshRenderGlobalParams.RayMarchIsoSurface = renderingParameters.RayMarchIsoSurface;
            meshRenderGlobalParams.UnderwaterRender = renderingParameters.UnderwaterRender ? 1 : 0;
            meshRenderGlobalParams.RayMarchMaxSteps = renderingParameters.RayMarchMaxSteps;
            meshRenderGlobalParams.RayMarchStepSize = renderingParameters.RayMarchStepSize;
            meshRenderGlobalParams.RayMarchStepFactor = renderingParameters.RayMarchStepFactor;
            Vector2 renderingResolution = resolution;
            Vector2 rayMarchResolution = renderingResolution * renderingParameters.RayMarchingResolutionDownscale;
            meshRenderGlobalParams.RayMarchResolution =
                new Vector2Int((int)rayMarchResolution.x, (int)rayMarchResolution.y);

            meshRenderGlobalParams.Absorption = new Vector4(0.0f, 0.0f, 0.0f, materialParameters.ScatteringAmount);

            Marshal.StructureToPtr(meshRenderGlobalParams, camMeshRenderParams[cam], true);

            Vector2 textureScale = new Vector2((float)resolution.x / resolution.x, (float)resolution.y / resolution.y);

            // update the data at the pointer
            Marshal.StructureToPtr(cameraRenderParams, camNativeParams[cam], true);

            if (ActiveRenderingMode == RenderingMode.MeshRender)
            {
                CurrentMaterial.SetMatrix("ProjectionInverse", cameraRenderParams.ProjectionInverse);
                CurrentMaterial.SetMatrix("ViewProjectionInverse", cameraRenderParams.ViewProjectionInverse);
                CurrentMaterial.SetMatrix("EyeRayCameraCoeficients", cameraRenderParams.EyeRayCameraCoeficients);

                CurrentMaterial.SetVector("TextureScale", textureScale);

                if (renderingParameters.UnderwaterRender)
                {
                    CurrentMaterial.EnableKeyword("UNDERWATER_RENDER");
                }
                else
                {
                    CurrentMaterial.DisableKeyword("UNDERWATER_RENDER");
                }
            }

            if (EnableDownscale)
            {
                CurrentUpscaleMaterial.SetVector("TextureScale", textureScale);
            }

            if (visualizeSceneSDF)
            {
                CurrentSDFRenderMaterial.SetVector("TextureScale", textureScale);
                CurrentSDFRenderMaterial.SetMatrix("EyeRayCameraCoeficients",
                                                   cameraRenderParams.EyeRayCameraCoeficients);
            }
        }

        /// <summary>
        /// Update render parameters for a given camera
        /// </summary>
        /// <param name="cam">Camera</param>
        public void InitializeNativeCameraParams(Camera cam)
        {
            if (!camNativeParams.ContainsKey(cam))
            {
                // allocate memory for camera parameters
                camNativeParams[cam] = Marshal.AllocHGlobal(Marshal.SizeOf(cameraRenderParams));
            }
            if (!camMeshRenderParams.ContainsKey(cam))
            {
                // allocate memory for mesh render parameters
                camMeshRenderParams[cam] = Marshal.AllocHGlobal(Marshal.SizeOf(meshRenderGlobalParams));
            }
        }

        public void UpdateNativeRenderParams()
        {
            // Needs to be specifically in this place, to make sure that render mode in Unity and in native plugin are
            // in sync
            ActiveRenderingMode = CurrentRenderingMode;

#if ZIBRA_LIQUID_DEBUG
            renderParams.NeuralSamplingDistance = materialParameters.NeuralSamplingDistance;
            renderParams.SDFDebug = materialParameters.SDFDebug;
#endif
            renderParams.RenderingMode = (int)ActiveRenderingMode;
            renderParams.VertexOptimizationIterations = renderingParameters.VertexOptimizationIterations;

            renderParams.MeshOptimizationIterations = renderingParameters.MeshOptimizationIterations;
            renderParams.DualContourIsoValue = renderingParameters.DualContourIsoSurfaceLevel;
            renderParams.MeshOptimizationStep = renderingParameters.MeshOptimizationStep;

            int maxVertexCount = numNodes;
            int maxTriangleCount =
                (int)(maxVertexCount * renderingParameters.MaxLiquidMeshSize / 3.0f + ADDITIONAL_VERTICES);

            renderParams.MaxVertexBufferSize = maxTriangleCount * 6;
            renderParams.MaxIndexBufferSize = maxTriangleCount * 3;

            GCHandle gcparamBuffer = GCHandle.Alloc(renderParams, GCHandleType.Pinned);

            solverCommandBuffer.Clear();
            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.SetRenderParameters,
                                                  gcparamBuffer.AddrOfPinnedObject());
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            gcparamBuffer.Free();
        }

        private void ClearCameraCommandBuffers()
        {
            // clear all rendering command buffers if not rendering
            foreach (KeyValuePair<Camera, CommandBuffer> entry in cameraCBs)
            {
                if (entry.Key != null)
                {
                    entry.Key.RemoveCommandBuffer(ActiveInjectionPoint, entry.Value);
                }
            }
            cameraCBs.Clear();
            cameras.Clear();
        }

        /// <summary>
        /// Rendering callback which is called by every camera in the scene
        /// </summary>
        /// <param name="cam">Camera</param>
        public void RenderCallBack(Camera cam, float renderPipelineRenderScale = 1.0f)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection ||
                cam.cameraType == CameraType.VR)
            {
                ClearCameraCommandBuffers();
                return;
            }

            UpdateCameraResolution(cam, renderPipelineRenderScale);

            // Need at least 2 simulation frames to start rendering
            if (!IsRenderingEnabled())
            {
                return;
            }

            if (!cameraResources.ContainsKey(cam))
            {
                cameraResources[cam] = new CameraResources();
            }

            // Re-add command buffers to cameras with new injection points
            if (CurrentInjectionPoint != ActiveInjectionPoint)
            {
                foreach (KeyValuePair<Camera, CommandBuffer> entry in cameraCBs)
                {
                    entry.Key.RemoveCommandBuffer(ActiveInjectionPoint, entry.Value);
                    entry.Key.AddCommandBuffer(CurrentInjectionPoint, entry.Value);
                }
                ActiveInjectionPoint = CurrentInjectionPoint;
            }

            bool visibleInCamera =
                (RenderPipelineDetector.GetRenderPipelineType() != RenderPipelineDetector.RenderPipeline.SRP) ||
                ((cam.cullingMask & (1 << this.gameObject.layer)) != 0);

            if (!isEnabled || !visibleInCamera || materialParameters.FluidMeshMaterial == null ||
                (EnableDownscale && materialParameters.UpscaleMaterial == null) ||
                (visualizeSceneSDF && materialParameters.SDFRenderMaterial == null))
            {
                if (cameraCBs.ContainsKey(cam))
                {
                    CameraEvent cameraEvent = (cam.actualRenderingPath == RenderingPath.Forward)
                                                  ? CameraEvent.BeforeForwardAlpha
                                                  : CameraEvent.AfterLighting;
                    cam.RemoveCommandBuffer(cameraEvent, cameraCBs[cam]);
                    cameraCBs[cam].Clear();
                    cameraCBs.Remove(cam);
                }

                return;
            }

            bool isDirty = SetMaterialParams(cam);
            isDirty = UpdateNativeTextures(cam, renderPipelineRenderScale) || isDirty;
            isDirty = !cameraCBs.ContainsKey(cam) || isDirty;
#if UNITY_EDITOR
            isDirty = isDirty || ForceRepaint;
#endif
            InitializeNativeCameraParams(cam);
            UpdateCamera(cam);

            if (RenderPipelineDetector.GetRenderPipelineType() != RenderPipelineDetector.RenderPipeline.SRP)
            {
#if UNITY_PIPELINE_HDRP || UNITY_PIPELINE_URP
                // upload camera parameters
                solverCommandBuffer.Clear();
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.SetCameraParameters,
                                                      camNativeParams[cam]);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);
#endif
            }
            else
            {
                if (!cameraCBs.ContainsKey(cam) || isDirty)
                {
                    CommandBuffer renderCommandBuffer;
                    if (isDirty && cameraCBs.ContainsKey(cam))
                    {
                        renderCommandBuffer = cameraCBs[cam];
                        renderCommandBuffer.Clear();
                    }
                    else
                    {
                        // Create render command buffer
                        renderCommandBuffer = new CommandBuffer { name = "ZibraLiquid.Render" };
                        // add command buffer to camera
                        cam.AddCommandBuffer(ActiveInjectionPoint, renderCommandBuffer);
                        // add camera to the list
                        cameraCBs[cam] = renderCommandBuffer;
                    }

                    // enable depth texture
                    cam.depthTextureMode = DepthTextureMode.Depth;

                    // update native camera parameters

                    if (IsBackgroundCopyNeeded(cam))
                    {
                        renderCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive,
                                                 cameraResources[cam].background);
                    }

                    if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
                    {
                        renderCommandBuffer.SetRenderTarget(
                            color0, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, depth,
                            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        renderCommandBuffer.ClearRenderTarget(true, true, Color.clear);
                    }
                    RenderLiquidNative(renderCommandBuffer, cam);
                    RenderFluid(renderCommandBuffer, cam);
                }
            }
        }

        ParticleSpeciesParameters GetSpeciesParametersDefault()
        {
            ParticleSpeciesParameters speciesParameters = new ParticleSpeciesParameters();
            speciesParameters.Gravity = solverParameters.Gravity / 100.0f;
            speciesParameters.AffineAmmount = 4.0f * (1.0f - solverParameters.Viscosity);
            speciesParameters.LiquidStiffness = solverParameters.FluidStiffness;
            speciesParameters.RestDensity = solverParameters.ParticleDensity;
#if ZIBRA_LIQUID_PAID_VERSION
            speciesParameters.SurfaceTension = solverParameters.SurfaceTension;
#endif
            speciesParameters.AffineDivergenceDecay = 1.0f;
            speciesParameters.VelocityLimit = solverParameters.MaximumVelocity;
            return speciesParameters;
        }

        void SetInteropBuffer<T>(IntPtr NativeBuffer, List<T> list)
        {
            long LongPtr = NativeBuffer.ToInt64(); // Must work both on x86 and x64
            for (int I = 0; I < list.Count; I++)
            {
                IntPtr Ptr = new IntPtr(LongPtr);
                Marshal.StructureToPtr(list[I], Ptr, true);
                LongPtr += Marshal.SizeOf(typeof(T));
            }
        }

        void UpdateInteropBuffers()
        {
            Marshal.StructureToPtr(fluidParameters, NativeFluidData, true);

            if (manipulatorManager.Elements > 0)
            {
                SetInteropBuffer(NativeManipData, manipulatorManager.ManipulatorParams);
            }

            if (manipulatorManager.SDFObjectList.Count > 0)
            {
                SetInteropBuffer(NativeSDFData, manipulatorManager.SDFObjectList);
            }

            List<ParticleSpeciesParameters> SpeciesList = new List<ParticleSpeciesParameters>();
            SpeciesList.Add(GetSpeciesParametersDefault());

            SetInteropBuffer(NativeSolverData, SpeciesList);
        }

        void UpdateSolverParameters()
        {
            // Update fluid parameters

            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.UpdateLiquidParameters, NativeFluidData);

            if (manipulatorManager.Elements > 0)
            {
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.UpdateManipulatorParameters,
                                                      NativeManipData);
            }

            if (manipulatorManager.SDFObjectList.Count > 0)
            {
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.UpdateSDFObjects, NativeSDFData);
            }

            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.UpdateSolverParameters, NativeSolverData);
        }

        public void RenderCallBackWrapper(Camera cam)
        {
            RenderCallBack(cam);
        }

        private void StepPhysics()
        {
            solverCommandBuffer.Clear();

            ForceCloseCommandEncoder(solverCommandBuffer);

            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.ClearSDFAndID);

            SetFluidParameters();

            manipulatorManager.UpdateDynamic(this, timestep / simTimePerSec);

            UpdateInteropBuffers();
            UpdateSolverParameters();

            // execute simulation
            ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                  ZibraLiquidBridge.EventID.StepPhysics);
            Graphics.ExecuteCommandBuffer(solverCommandBuffer);

            // update internal time
            simulationInternalTime += timestep;
            simulationInternalFrame++;
        }

        void UpdateManipulatorStatistics()
        {
#if ZIBRA_LIQUID_PAID_VERSION
            /// ManipulatorStatistics GPUReadback
            if (!IsSimulationEnabled() || manipulatorManager.Elements == 0)
            {
                return;
            }

            UInt32 size = (UInt32)manipulatorManager.Elements * STATISTICS_PER_MANIPULATOR;
            IntPtr readbackData = ZibraLiquidBridge.GPUReadbackGetData(CurrentInstanceID, size * sizeof(Int32));
            if (readbackData != IntPtr.Zero)
            {
                Int32[] Stats = new Int32[size];
                Marshal.Copy(readbackData, Stats, 0, (Int32)size);
                manipulatorManager.UpdateStatistics(Stats, manipulators, solverParameters, sdfColliders);
            }
#endif
        }

        // stability calibration curve fit
        private float DivergenceDecayCurve(float x)
        {
            float a = (0.177f - 0.85f * x + 9.0f * x * x) / 1.8f;
            return 1.8f * a / (a + 1);
        }

        private void SetFluidParameters()
        {
            solverParameters.ValidateParameters();
            containerPos = transform.position;

            fluidParameters.GridSize = GridSize;
            fluidParameters.ContainerScale = containerSize;
            fluidParameters.NodeCount = numNodes;
            fluidParameters.ContainerPos = containerPos;
            fluidParameters.TimeStep = timestep;

            fluidParameters.SimulationFrame = simulationInternalFrame;
            fluidParameters.DensityBlurRadius = materialParameters.FluidSurfaceBlur;
            fluidParameters.LiquidIsosurfaceThreshold = renderingParameters.IsoSurfaceLevel;
            fluidParameters.VertexOptimizationStep = renderingParameters.VertexOptimizationStep;

            // ParticleTranslation is set by native plugin

            float MaxVelocityLimit = solverParameters.MaximumVelocity;
            fluidParameters.GlobalVelocityLimit = MaxVelocityLimit;

#if ZIBRA_LIQUID_PAID_VERSION
            fluidParameters.MinimumVelocity = solverParameters.MinimumVelocity;
#endif
            // BlurNormalizationConstant set by native plugin
            fluidParameters.MaxParticleCount = MaxNumParticles;
            fluidParameters.VisualizeSDF = visualizeSceneSDF ? 1 : 0;

            fluidParameters.SimulationTime = simulationInternalTime;
            fluidParameters.ParticleSpeciesCount = 1;
        }

        /// <summary>
        /// Disable fluid render for a given camera
        /// </summary>
        public void DisableForCamera(Camera cam)
        {
            CameraEvent cameraEvent =
                cam.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterSkybox : CameraEvent.AfterLighting;
            cam.RemoveCommandBuffer(cameraEvent, cameraCBs[cam]);
            cameraCBs[cam].Dispose();
            cameraCBs.Remove(cam);
        }

        protected void ClearRendering()
        {
            Camera.onPreRender -= RenderCallBackWrapper;

            ClearCameraCommandBuffers();

            // free allocated memory
            foreach (var data in camNativeParams)
            {
                Marshal.FreeHGlobal(data.Value);
            }

            // TODO
            // Fix memory cleanup
            // Can't currently release this data, since it may be used on render thread
            // Unity doesn't allow us to execute C# code on render thread
            // foreach (var data in camMeshRenderParams)
            //{
            //    Marshal.FreeHGlobal(data.Value);
            //}
            // foreach (var data in toFreeOnExit)
            //{
            //    Marshal.FreeHGlobal(data);
            //}

            foreach (var resource in cameraResources)
            {
                if (resource.Value.background != null)
                {
                    resource.Value.background.Release();
                    resource.Value.background = null;
                }
            }

            cameraResources.Clear();

            ZibraLiquidGPUGarbageCollector.SafeRelease(color0);
            color0 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(color1);
            color1 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(color2);
            color2 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(upscaleColor);
            upscaleColor = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(VertexIDGrid);
            VertexIDGrid = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(VertexBuffer0);
            VertexBuffer0 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(VertexBuffer1);
            VertexBuffer1 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(TransferDataBuffer);
            TransferDataBuffer = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(MeshRenderIndexBuffer);
            MeshRenderIndexBuffer = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(QuadBuffer);
            QuadBuffer = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(VertexProperties);
            VertexProperties = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(GridNormalTexture);
            GridNormalTexture = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(DensityTexture);
            DensityTexture = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(VelocityTexture);
            VelocityTexture = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(SDFGridTexture);
            SDFGridTexture = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(EmbeddingsTexture);
            EmbeddingsTexture = null;
            camNativeParams.Clear();
        }

        protected void ClearSolver()
        {
            if (solverCommandBuffer != null)
            {
                ZibraLiquidBridge.SubmitInstanceEvent(solverCommandBuffer, CurrentInstanceID,
                                                      ZibraLiquidBridge.EventID.ReleaseResources);
                Graphics.ExecuteCommandBuffer(solverCommandBuffer);
            }

            if (solverCommandBuffer != null)
            {
                solverCommandBuffer.Release();
                solverCommandBuffer = null;
            }

            ZibraLiquidGPUGarbageCollector.SafeRelease(PositionMass);
            PositionMass = null;
            if (Affine != null)
            {
                ZibraLiquidGPUGarbageCollector.SafeRelease(Affine[0]);
                Affine[0] = null;
                ZibraLiquidGPUGarbageCollector.SafeRelease(Affine[1]);
                Affine[1] = null;
            }
            ZibraLiquidGPUGarbageCollector.SafeRelease(GridData);
            GridData = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(IndexGrid);
            IndexGrid = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(nodeParticlePairs0);
            nodeParticlePairs0 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(nodeParticlePairs1);
            nodeParticlePairs1 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(RadixGroupData1);
            RadixGroupData1 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(RadixGroupData2);
            RadixGroupData2 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(RadixGroupData3);
            RadixGroupData3 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(positionMassCopy);
            positionMassCopy = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(GridNormal);
            GridNormal = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(GridBlur0);
            GridBlur0 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(GridBlur1);
            GridBlur1 = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(GridSDF);
            GridSDF = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(ParticleNumber);
            ParticleNumber = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(DynamicManipulatorData);
            DynamicManipulatorData = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(ParticleSpeciesData);
            ParticleSpeciesData = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(SDFObjectData);
            SDFObjectData = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(ManipulatorStatistics);
            ManipulatorStatistics = null;
            ZibraLiquidGPUGarbageCollector.SafeRelease(Counters);
            Counters = null;
            if (!Application.isEditor)
            {
                Destroy(LiquidMesh);
            }
            else
            {
                DestroyImmediate(LiquidMesh);
            }

            Marshal.FreeHGlobal(NativeManipData);
            NativeManipData = IntPtr.Zero;
            Marshal.FreeHGlobal(NativeFluidData);
            NativeFluidData = IntPtr.Zero;

            CurrentTextureResolution = new Vector2Int(0, 0);
            GridSize = new Vector3Int(0, 0, 0);
            activeParticleNumber = 0;
            numNodes = 0;
            particleDiameter = 0.0f;
            simulationInternalFrame = 0;
            simulationInternalTime = 0.0f;
            timestep = 0.0f;
            camRenderResolutions.Clear();
            camNativeResolutions.Clear();

            initialized = false;

            // DO NOT USE AllFluids.Remove(this)
            // This will not result in equivalent code
            // ZibraLiquid::Equals is overriden and don't have correct implementation

            if (AllFluids != null)
            {
                for (int i = 0; i < AllFluids.Count; i++)
                {
                    var fluid = AllFluids[i];
                    if (ReferenceEquals(fluid, this))
                    {
                        AllFluids.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public float GetParticleSize()
        {
            return (float)(CellSize / Math.Pow(solverParameters.ParticleDensity, 1.0f / 3.0f));
        }

        public ReadOnlyCollection<ZibraLiquidCollider> GetColliderList()
        {
            return sdfColliders.AsReadOnly();
        }

        public bool HasCollider(ZibraLiquidCollider collider)
        {
            return sdfColliders.Contains(collider);
        }

        public void AddCollider(ZibraLiquidCollider collider)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (!sdfColliders.Contains(collider))
            {
                sdfColliders.Add(collider);
                sdfColliders.Sort(new SDFColliderCompare());
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        public void RemoveCollider(ZibraLiquidCollider collider)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (sdfColliders.Contains(collider))
            {
                sdfColliders.Remove(collider);
                sdfColliders.Sort(new SDFColliderCompare());
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        public bool HasEmitter()
        {
            foreach (var manipulator in manipulators)
            {
                if (manipulator.GetManipulatorType() == Manipulator.ManipulatorType.Emitter)
                {
                    return true;
                }
            }

            return false;
        }

        public ReadOnlyCollection<Manipulator> GetManipulatorList()
        {
            return manipulators.AsReadOnly();
        }

        public bool HasManipulator(Manipulator manipulator)
        {
            return manipulators.Contains(manipulator);
        }

        public void AddManipulator(Manipulator manipulator)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (!manipulators.Contains(manipulator))
            {
                manipulators.Add(manipulator);
                manipulators.Sort(new ManipulatorCompare());
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
#if UNITY_EDITOR
            OnValidate();
#endif
        }

        public void RemoveManipulator(Manipulator manipulator)
        {
            if (initialized)
            {
                Debug.LogWarning("We don't yet support changing number of manipulators/colliders at runtime.");
                return;
            }

            if (manipulators.Contains(manipulator))
            {
                manipulators.Remove(manipulator);
                manipulators.Sort(new ManipulatorCompare());
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            switch (CurrentRenderingMode)
            {
                case RenderingMode.MeshRender:
                case RenderingMode.UnityRender:
                    break;
                default:
                    CurrentRenderingMode = RenderingMode.MeshRender;
                    UnityEditor.EditorUtility.SetDirty(this);
                    break;
            }

            containerSize[0] = Math.Max(containerSize[0], 1e-3f);
            containerSize[1] = Math.Max(containerSize[1], 1e-3f);
            containerSize[2] = Math.Max(containerSize[2], 1e-3f);

            CellSize = Math.Max(containerSize.x, Math.Max(containerSize.y, containerSize.z)) / gridResolution;

            if (GetComponent<ZibraLiquidMaterialParameters>() == null)
            {
                gameObject.AddComponent<ZibraLiquidMaterialParameters>();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (GetComponent<ZibraLiquidSolverParameters>() == null)
            {
                gameObject.AddComponent<ZibraLiquidSolverParameters>();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (GetComponent<ZibraLiquidAdvancedRenderParameters>() == null)
            {
                gameObject.AddComponent<ZibraLiquidAdvancedRenderParameters>();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (GetComponent<ZibraManipulatorManager>() == null)
            {
                gameObject.AddComponent<ZibraManipulatorManager>();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (sdfColliders != null)
            {
                int removed = sdfColliders.RemoveAll(item => item == null);
                if (removed > 0) {
                    sdfColliders.Sort(new SDFColliderCompare());
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }

            if (manipulators != null)
            {
                int removed = manipulators.RemoveAll(item => item == null);
                if (removed > 0) {
                    manipulators.Sort(new ManipulatorCompare());
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }

#if ZIBRA_LIQUID_PAID_VERSION
            if (BakedInitialStateAsset)
            {
                int bakedLiquidHeader = BitConverter.ToInt32(BakedInitialStateAsset.bytes, 0);
                if (bakedLiquidHeader != BAKED_LIQUID_HEADER_VALUE)
                {
                    BakedInitialStateAsset = null;
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
#endif

#if !ZIBRA_LIQUID_PAID_VERSION
            List<Manipulator> newManips = new List<Manipulator>();

            int forceFieldsFound = 0;
            int emittersFound = 0;
            foreach (Manipulator manip in manipulators)
            {
                if (manip is ZibraLiquidForceField)
                {
                    if (forceFieldsFound == 0) newManips.Add(manip);
                    ++forceFieldsFound;
                } 
                else if (manip is ZibraLiquidEmitter)
                {
                    if (emittersFound == 0) newManips.Add(manip);
                    ++emittersFound;
                }
            }
            manipulators = newManips;
            
            if (forceFieldsFound > 1) 
                Debug.LogWarning("Too many force fields for free version of Zibra Liquids, some force fields will be disabled. Free version limited to 1 force field.");
            if (emittersFound > 1) 
                Debug.LogWarning("Too many emitters for free version of Zibra Liquids, some emitters will be disabled. Free version limited to 1 emitter.");

            if (sdfColliders.Count > 5)
            {
                Debug.LogWarning(
                    "Too many SDF colliders for free version of Zibra Liquids, some colliders will be disabled. Free version limited to 5 SDF colliders.");
                sdfColliders.RemoveRange(5, sdfColliders.Count - 5);
            }
#endif
        }
#endif

        protected void OnApplicationQuit()
        {
            // On quit we need to destroy liquid before destroying any colliders/manipulators
            OnDisable();
        }

        public void StopSolver()
        {
            if (!initialized)
            {
                return;
            }

            // TODO remove this after beta test

            initialized = false;
            ClearRendering();
            ClearSolver();
            isEnabled = false;

            // If ZibraLiquid object gets disabled/destroyed
            // We still may need to do cleanup few frames later
            // So we create new gameobject which allows us to run cleanup code
            ZibraLiquidGPUGarbageCollector.CreateGarbageCollector();
        }

        // dispose the objects
        protected void OnDisable()
        {
            StopSolver();
        }

        private float ByteArrayToSingle(byte[] array, ref int startIndex)
        {
            float value = BitConverter.ToSingle(array, startIndex);
            startIndex += sizeof(float);
            return value;
        }

        private int ByteArrayToInt(byte[] array, ref int startIndex)
        {
            int value = BitConverter.ToInt32(array, startIndex);
            startIndex += sizeof(int);
            return value;
        }

#if ZIBRA_LIQUID_PAID_VERSION
        private BakedInitialState ConvertBytesToInitialState(byte[] data)
        {
            int startIndex = 0;

            int header = ByteArrayToInt(data, ref startIndex);
            if (header != BAKED_LIQUID_HEADER_VALUE)
            {
                throw new Exception("Invalid baked liquid data.");
            }

            int particleCount = ByteArrayToInt(data, ref startIndex);
            if (particleCount > MaxNumParticles)
            {
                throw new Exception("Baked data have more particles than max particle count.");
            }

            BakedInitialState initialStateData = new BakedInitialState();
            initialStateData.ParticleCount = particleCount;
            initialStateData.Positions = new Vector4[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    initialStateData.Positions[i][j] = ByteArrayToSingle(data, ref startIndex);
                }

                initialStateData.Positions[i].w = 1.0f;
            }

            initialStateData.AffineVelocity = new Vector2Int[4 * particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    initialStateData.AffineVelocity[4 * i + 3][j] = ByteArrayToInt(data, ref startIndex);
                }
            }

            return initialStateData;
        }

        private BakedInitialState LoadInitialStateAsset()
        {
            byte[] data = BakedInitialStateAsset.bytes;
            return ConvertBytesToInitialState(data);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Save current simulation state
        /// </summary>
        public BakedInitialState SerializeCurrentLiquidState()
        {
            int[] ParticleNumberArray = new int[1];
            ParticleNumber.GetData(ParticleNumberArray, 0, 0, 1);

            BakedInitialState initialStateData = new BakedInitialState();

            initialStateData.ParticleCount = ParticleNumberArray[0];

            int currentAffineIndex = 1 - ZibraLiquidBridge.GetCurrentAffineBufferIndex(CurrentInstanceID);

            InitialState = InitialStateType.BakedLiquidState;
            Array.Resize(ref initialStateData.Positions, initialStateData.ParticleCount);
            PositionMass.GetData(initialStateData.Positions);
            Array.Resize(ref initialStateData.AffineVelocity, 4 * initialStateData.ParticleCount);
            Affine[currentAffineIndex].GetData(initialStateData.AffineVelocity);

            ForceRepaint = true;

            return initialStateData;
        }
#endif

        /// <summary>
        /// Apply currently set initial conditions
        /// </summary>
        protected void ApplyInitialState()
        {
            switch (InitialState)
            {
            case InitialStateType.NoParticles:
                fluidParameters.ParticleCount = 0;
                break;
            case InitialStateType.BakedLiquidState:
                if (BakedInitialStateAsset)
                {
                    BakedInitialState initialStateData = LoadInitialStateAsset();
                    PositionMass.SetData(initialStateData.Positions);
                    Affine[0].SetData(initialStateData.AffineVelocity);
                    Affine[1].SetData(initialStateData.AffineVelocity);
                    fluidParameters.ParticleCount = initialStateData.ParticleCount;
                }
                else
                {
                    fluidParameters.ParticleCount = 0;
                }

                break;
            }
        }
#endif

        private Matrix4x4 CalculateEyeRayCameraCoeficients(Camera cam)
        {
            float fovTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (cam.orthographic)
            {
                fovTan = 0.0f;
            }
            Vector3 r = cam.transform.right * cam.aspect * fovTan;
            Vector3 u = -cam.transform.up * fovTan;
            Vector3 v = cam.transform.forward;

            return new Matrix4x4(new Vector4(r.x, r.y, r.z, 0.0f), new Vector4(u.x, u.y, u.z, 0.0f),
                                 new Vector4(v.x, v.y, v.z, 0.0f), new Vector4(0.0f, 0.0f, 0.0f, 0.0f))
                .transpose;
        }

        public ZibraLiquidCollider HasGivenCollider(GameObject collider)
        {
            foreach (var col in sdfColliders)
            {
                if (col.gameObject == collider)
                {
                    return col;
                }
            }
            return null;
        }
    }
}