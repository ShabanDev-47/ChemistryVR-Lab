using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using com.zibra.liquid.Solver;
using com.zibra.liquid.SDFObjects;
using com.zibra.liquid.Utilities;

namespace com.zibra.liquid.Manipulators
{
    public class ZibraManipulatorManager : MonoBehaviour
    {
        [HideInInspector]
        [StructLayout(LayoutKind.Sequential)]
        public struct ManipulatorParam
        {
            public Int32 Enabled;
            public Int32 SDFObjectID;
            public Int32 ParticleSpecies;
            public Int32 IntParameter;

            public Vector4 AdditionalData0;
            public Vector4 AdditionalData1;
        }

        [HideInInspector]
        [StructLayout(LayoutKind.Sequential)]
        public struct SDFObjectParams
        {
            public Vector3 Position;
            public Single NormalSmooth;

            public Vector3 Velocity;
            public Single SurfaceValue;

            public Vector3 Scale;
            public Single DistanceScale;

            public Vector3 AnguralVelocity;
            public Int32 Type;

            public Quaternion Rotation;

            public Vector3 BBoxSize;
            public Single BBoxVolume;

            public Int32 EmbeddingTextureBlocks;
            public Int32 SDFTextureBlocks;
            public Int32 ObjectID;
            public Single TotalGroupVolume;
        };

        [HideInInspector]
        [StructLayout(LayoutKind.Sequential)]
        public struct ManipulatorIndices
        {
            public Int32 EmitterIndexBegin;
            public Int32 EmitterIndexEnd;
            public Int32 VoidIndexBegin;
            public Int32 VoidIndexEnd;

            public Int32 ForceFieldIndexBegin;
            public Int32 ForceFieldIndexEnd;
            public Int32 AnalyticColliderIndexBegin;
            public Int32 AnalyticColliderIndexEnd;

            public Int32 NeuralColliderIndexBegin;
            public Int32 NeuralColliderIndexEnd;
            public Int32 GroupColliderIndexBegin;
            public Int32 GroupColliderIndexEnd;

            public Int32 DetectorIndexBegin;
            public Int32 DetectorIndexEnd;
            public Int32 SpeciesModifierIndexBegin;
            public Int32 SpeciesModifierIndexEnd;

            public Int32 PortalIndexBegin;
            public Int32 PortalIndexEnd;
            public Vector2Int IndexPadding;
        }

        int[] TypeIndex = new int[(int)Manipulator.ManipulatorType.TypeNum + 1];

        public ManipulatorIndices indices = new ManipulatorIndices();

        // All data together
        [HideInInspector]
        public int Elements = 0;
        [HideInInspector]
        public List<ManipulatorParam> ManipulatorParams = new List<ManipulatorParam>();
        [HideInInspector]
        public List<SDFObjectParams> SDFObjectList = new List<SDFObjectParams>();
        [HideInInspector]
        public Color32[] Embeddings;
        [HideInInspector]
        public byte[] SDFGrid;
        [HideInInspector]
        public List<int> ConstDataID = new List<int>();

        [HideInInspector]
        public int TextureCount = 0;
        [HideInInspector]
        public int SDFTextureSize = 0;
        [HideInInspector]
        public int EmbeddingTextureSize = 0;

        [HideInInspector]
        public int SDFTextureBlocks = 0;
        [HideInInspector]
        public int EmbeddingTextureBlocks = 0;

        [HideInInspector]
        public int SDFTextureDimension = 0;
        [HideInInspector]
        public int EmbeddingTextureDimension = 0;

#if ZIBRA_LIQUID_PAID_VERSION
        [HideInInspector]
        public Dictionary<ZibraHash128, NeuralSDF> neuralSDFs = new Dictionary<ZibraHash128, NeuralSDF>();
        [HideInInspector]
        public Dictionary<ZibraHash128, int> textureHashMap = new Dictionary<ZibraHash128, int>();
#endif

        private List<Manipulator> manipulators;

        private Vector3 Abs(Vector3 x)
        {
            return new Vector3(Mathf.Abs(x.x), Mathf.Abs(x.y), Mathf.Abs(x.z));
        }

        protected SDFObjectParams GetSDF(SDFObject obj, Manipulator manipulator)
        {
            SDFObjectParams sdf = new SDFObjectParams();

            if (obj == null)
            {
                throw new Exception("Missing SDF on manipulator");
            }

            sdf.Rotation = manipulator.GetRotation();
            sdf.Scale = manipulator.GetScale();
            sdf.Position = manipulator.GetPosition();
            sdf.BBoxSize = 2.0f * sdf.Scale;

            sdf.NormalSmooth = 0.01f;
            sdf.Velocity = Vector3.zero;
            sdf.SurfaceValue = 0.0f;
            SDFObject main = manipulator.GetComponent<SDFObject>();
            if (main != null)
            {
                sdf.SurfaceValue += main.SurfaceDistance;
            }
            sdf.SurfaceValue += obj.SurfaceDistance;
            sdf.DistanceScale = 1.0f;
            sdf.AnguralVelocity = Vector3.zero;
            sdf.Type = 0;
            sdf.TotalGroupVolume = 0.0f;
            sdf.BBoxSize = 0.5f * manipulator.transform.lossyScale;

#if ZIBRA_LIQUID_PAID_VERSION
            if (manipulator is ZibraLiquidEmitter || manipulator is ZibraLiquidVoid)
#else
            if (manipulator is ZibraLiquidEmitter)
#endif
            {
                // use Box as default
                sdf.Type = 1;
            }

            if (obj is AnalyticSDF)
            {
                AnalyticSDF analyticSDF = obj as AnalyticSDF;
                sdf.Type = (int)analyticSDF.chosenSDFType;
                sdf.DistanceScale = analyticSDF.InvertSDF ? -1.0f : 1.0f;
                sdf.BBoxSize = analyticSDF.GetBBoxSize();
            }

#if ZIBRA_LIQUID_PAID_VERSION
            if (obj is NeuralSDF)
            {
                NeuralSDF neuralSDF = obj as NeuralSDF;
                Matrix4x4 transf = obj.transform.localToWorldMatrix * neuralSDF.objectRepresentation.ObjectTransform;

                sdf.Rotation = transf.rotation;
                sdf.Scale = Abs(transf.lossyScale) * (1.0f + 0.1f);
                sdf.Position = transf.MultiplyPoint(Vector3.zero);
                sdf.Type = -1;
                sdf.ObjectID = textureHashMap[neuralSDF.objectRepresentation.GetHash()];
                sdf.EmbeddingTextureBlocks = EmbeddingTextureBlocks;
                sdf.SDFTextureBlocks = SDFTextureBlocks;
                sdf.DistanceScale = neuralSDF.InvertSDF ? -1.0f : 1.0f;
                sdf.BBoxSize = sdf.Scale;
            }
#endif

            sdf.BBoxVolume = sdf.BBoxSize.x * sdf.BBoxSize.y * sdf.BBoxSize.z;
            return sdf;
        }

#if ZIBRA_LIQUID_PAID_VERSION
        protected void AddTexture(NeuralSDF neuralSDF)
        {
            ZibraHash128 curHash = neuralSDF.objectRepresentation.GetHash();

            if (textureHashMap.ContainsKey(curHash))
                return;

            SDFTextureSize +=
                neuralSDF.objectRepresentation.GridResolution / NeuralSDFRepresentation.BLOCK_SDF_APPROX_DIMENSION;
            EmbeddingTextureSize += NeuralSDFRepresentation.EMBEDDING_SIZE *
                                    neuralSDF.objectRepresentation.EmbeddingResolution /
                                    NeuralSDFRepresentation.BLOCK_EMBEDDING_GRID_DIMENSION;
            neuralSDFs[curHash] = neuralSDF;

            int sdfID = TextureCount;
            textureHashMap[curHash] = sdfID;

            TextureCount++;
        }

        protected void AddTextureData(NeuralSDF neuralSDF)
        {
            ZibraHash128 curHash = neuralSDF.objectRepresentation.GetHash();
            int sdfID = textureHashMap[curHash];

            // Embedding texture
            for (int t = 0; t < NeuralSDFRepresentation.EMBEDDING_SIZE; t++)
            {
                int block = sdfID * NeuralSDFRepresentation.EMBEDDING_SIZE + t;
                Vector3Int blockPos = NeuralSDFRepresentation.BLOCK_EMBEDDING_GRID_DIMENSION *
                                      new Vector3Int(block % EmbeddingTextureBlocks,
                                                     (block / EmbeddingTextureBlocks) % EmbeddingTextureBlocks,
                                                     block / (EmbeddingTextureBlocks * EmbeddingTextureBlocks));
                int Size = neuralSDF.objectRepresentation.EmbeddingResolution;

                for (int i = 0; i < Size; i++)
                {
                    for (int j = 0; j < Size; j++)
                    {
                        for (int k = 0; k < Size; k++)
                        {
                            Vector3Int pos = blockPos + new Vector3Int(i, j, k);
                            int id = pos.x + EmbeddingTextureDimension * (pos.y + EmbeddingTextureDimension * pos.z);
                            if (id >= EmbeddingTextureSize)
                            {
                                Debug.LogError(pos);
                            }
                            Embeddings[id] = neuralSDF.objectRepresentation.GetEmbedding(i, j, k, t);
                        }
                    }
                }
            }

            // SDF approximation texture
            {
                int block = sdfID;
                Vector3Int blockPos =
                    NeuralSDFRepresentation.BLOCK_SDF_APPROX_DIMENSION *
                    new Vector3Int(block % SDFTextureBlocks, (block / SDFTextureBlocks) % SDFTextureBlocks,
                                   block / (SDFTextureBlocks * SDFTextureBlocks));
                int Size = neuralSDF.objectRepresentation.GridResolution;
                for (int i = 0; i < Size; i++)
                {
                    for (int j = 0; j < Size; j++)
                    {
                        for (int k = 0; k < Size; k++)
                        {
                            Vector3Int pos = blockPos + new Vector3Int(i, j, k);
                            int id = pos.x + SDFTextureDimension * (pos.y + SDFTextureDimension * pos.z);
                            for (int t = 0; t < 2; t++)
                                SDFGrid[2 * id + t] = neuralSDF.objectRepresentation.GetSDGrid(i, j, k, t);
                        }
                    }
                }
            }
        }

        protected void CalculateTextureData()
        {
            SDFTextureBlocks = (int)Mathf.Ceil(Mathf.Pow(SDFTextureSize, (1.0f / 3.0f)));
            EmbeddingTextureBlocks = (int)Mathf.Ceil(Mathf.Pow(EmbeddingTextureSize, (1.0f / 3.0f)));

            SDFTextureDimension = NeuralSDFRepresentation.BLOCK_SDF_APPROX_DIMENSION * SDFTextureBlocks;
            EmbeddingTextureDimension = NeuralSDFRepresentation.BLOCK_EMBEDDING_GRID_DIMENSION * EmbeddingTextureBlocks;

            SDFTextureSize = SDFTextureDimension * SDFTextureDimension * SDFTextureDimension;
            EmbeddingTextureSize = EmbeddingTextureDimension * EmbeddingTextureDimension * EmbeddingTextureDimension;

            Array.Resize<Color32>(ref Embeddings, EmbeddingTextureSize);
            Array.Resize<byte>(ref SDFGrid, 2 * SDFTextureSize);

            foreach (var sdf in neuralSDFs.Values)
            {
                AddTextureData(sdf);
            }
        }

#endif

        /// <summary>
        /// Update all arrays and lists with manipulator object data
        /// Should be executed every simulation frame
        /// </summary>
        ///
        public void UpdateDynamic(ZibraLiquid parent, float deltaTime = 0.0f)
        {
            Vector3 containerPos = parent.containerPos;
            Vector3 containerSize = parent.containerSize;

            int ID = 0;
            ManipulatorParams.Clear();
            SDFObjectList.Clear();
            // fill arrays

            foreach (var manipulator in manipulators)
            {
                if (manipulator == null)
                    continue;

                ManipulatorParam manip = new ManipulatorParam();

                manip.Enabled = (manipulator.isActiveAndEnabled && manipulator.gameObject.activeInHierarchy) ? 1 : 0;
                manip.AdditionalData0 = manipulator.AdditionalData0;
                manip.AdditionalData1 = manipulator.AdditionalData1;

                SDFObjectParams sdf = GetSDF(manipulator.GetComponent<SDFObject>(), manipulator);

                if (manipulator is ZibraLiquidEmitter)
                {
                    ZibraLiquidEmitter emitter = manipulator as ZibraLiquidEmitter;

                    float particlesPerSec = emitter.VolumePerSimTime / parent.CellSize / parent.CellSize /
                                            parent.CellSize * parent.solverParameters.ParticleDensity *
                                            parent.simTimePerSec;

                    manip.AdditionalData0.x = Mathf.Floor(particlesPerSec * deltaTime);
                }

                manip.SDFObjectID = SDFObjectList.Count;
                SDFObjectList.Add(sdf);
                ManipulatorParams.Add(manip);
                ID++;
            }

            Elements = manipulators.Count;
        }

        private static float INT2Float(int a)
        {
            const float MAX_INT = 2147483647.0f;
            const float F2I_MAX_VALUE = 5000.0f;
            const float F2I_SCALE = (MAX_INT / F2I_MAX_VALUE);

            return a / F2I_SCALE;
        }

        private int GetStatIndex(int id, int offset)
        {
            return id * Solver.ZibraLiquid.STATISTICS_PER_MANIPULATOR + offset;
        }

#if ZIBRA_LIQUID_PAID_VERSION
        /// <summary>
        /// Update manipulator statistics
        /// </summary>
        public void UpdateStatistics(Int32[] data, List<Manipulator> curManipulators,
                                     DataStructures.ZibraLiquidSolverParameters solverParameters,
                                     List<ZibraLiquidCollider> sdfObjects)
        {
            int id = 0;
            foreach (var manipulator in manipulators)
            {
                if (manipulator == null)
                    continue;

                Vector3 Force = Mathf.Exp(4.0f * solverParameters.ForceInteractionStrength) *
                                new Vector3(INT2Float(data[GetStatIndex(id, 0)]), INT2Float(data[GetStatIndex(id, 1)]),
                                            INT2Float(data[GetStatIndex(id, 2)]));
                Vector3 Torque = Mathf.Exp(4.0f * solverParameters.ForceInteractionStrength) *
                                 new Vector3(INT2Float(data[GetStatIndex(id, 3)]), INT2Float(data[GetStatIndex(id, 4)]),
                                             INT2Float(data[GetStatIndex(id, 5)]));

                switch (manipulator.GetManipulatorType())
                {
                default:
                    break;
                case Manipulator.ManipulatorType.Emitter:
                    ZibraLiquidEmitter emitter = manipulator as ZibraLiquidEmitter;
                    emitter.createdParticlesPerFrame = data[GetStatIndex(id, 0)];
                    emitter.createdParticlesTotal += emitter.createdParticlesPerFrame;
                    break;
                case Manipulator.ManipulatorType.Void:
                    ZibraLiquidVoid zibravoid = manipulator as ZibraLiquidVoid;
                    zibravoid.deletedParticleCountPerFrame = data[GetStatIndex(id, 0)];
                    zibravoid.deletedParticleCountTotal += zibravoid.deletedParticleCountPerFrame;
                    break;
                case Manipulator.ManipulatorType.Detector:
                    ZibraLiquidDetector zibradetector = manipulator as ZibraLiquidDetector;
                    zibradetector.particlesInside = data[GetStatIndex(id, 0)];
                    break;
                case Manipulator.ManipulatorType.NeuralCollider:
                case Manipulator.ManipulatorType.AnalyticCollider:
                    ZibraLiquidCollider collider = manipulator as ZibraLiquidCollider;
                    collider.ApplyForceTorque(Force, Torque);
                    break;
                }
#if UNITY_EDITOR
                manipulator.NotifyChange();
#endif

                id++;
            }
        }
#endif

        /// <summary>
        /// Update constant object data and generate and sort the current manipulator list
        /// Should be executed once
        /// </summary>
        public void UpdateConst(List<Manipulator> curManipulators, List<ZibraLiquidCollider> colliders)
        {
            manipulators = new List<Manipulator>();

#if ZIBRA_LIQUID_PAID_VERSION
            neuralSDFs = new Dictionary<ZibraHash128, NeuralSDF>();
            textureHashMap = new Dictionary<ZibraHash128, int>();
#endif

            // add all colliders to the manipulator list
            foreach (var manipulator in curManipulators)
            {
                if (manipulator == null)
                    continue;

                var sdf = manipulator.GetComponent<SDFObject>();
                if (sdf == null)
                {
                    Debug.LogWarning("Manipulator " + manipulator.gameObject.name + " missing sdf and is disabled.");
                    continue;
                }

#if ZIBRA_LIQUID_PAID_VERSION
                if (sdf is NeuralSDF)
                {
                    NeuralSDF neuralSDF = manipulator.GetComponent<NeuralSDF>();
                    if (neuralSDF != null && !neuralSDF.objectRepresentation.HasRepresentationV3)
                    {
                        Debug.LogWarning(
                            "Manipulator " + manipulator.gameObject.name +
                            " tries to use Neural SDF which is unsupported in this version, and is disabled.");
                        continue;
                    }
                }
#endif

                manipulators.Add(manipulator);
            }

            // add all colliders to the manipulator list
            foreach (var manipulator in colliders)
            {
                if (manipulator == null)
                    continue;

                var sdf = manipulator.GetComponent<SDFObject>();
                if (sdf == null)
                {
                    Debug.LogWarning("Collider " + manipulator.gameObject.name + " missing sdf and is disabled.");
                    continue;
                }

#if ZIBRA_LIQUID_PAID_VERSION
                NeuralSDF neuralSDF = manipulator.GetComponent<NeuralSDF>();
                if (neuralSDF != null && !neuralSDF.objectRepresentation.HasRepresentationV3)
                {
                    Debug.LogWarning("Neural collider " + manipulator.gameObject.name +
                                     " was not generated and is disabled.");
                    continue;
                }
#endif

                manipulators.Add(manipulator);
            }

            // first sort the manipulators
            manipulators.Sort(new ManipulatorCompare());

            // compute prefix sum
            for (int i = 0; i < (int)Manipulator.ManipulatorType.TypeNum; i++)
            {
                int id = 0;
                foreach (var manipulator in manipulators)
                {
                    if ((int)manipulator.GetManipulatorType() >= i)
                    {
                        TypeIndex[i] = id;
                        break;
                    }
                    id++;
                }

                if (id == manipulators.Count)
                {
                    TypeIndex[i] = manipulators.Count;
                }
            }

            // set last as the total number of manipulators
            TypeIndex[(int)Manipulator.ManipulatorType.TypeNum] = manipulators.Count;

            indices.EmitterIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.Emitter];
            indices.EmitterIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Emitter + 1];
            indices.VoidIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.Void];
            indices.VoidIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Void + 1];
            indices.ForceFieldIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.ForceField];
            indices.ForceFieldIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.ForceField + 1];
            indices.AnalyticColliderIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.AnalyticCollider];
            indices.AnalyticColliderIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.AnalyticCollider + 1];
            indices.NeuralColliderIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.NeuralCollider];
            indices.NeuralColliderIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.NeuralCollider + 1];
            indices.GroupColliderIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.GroupCollider];
            indices.GroupColliderIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.GroupCollider + 1];
            indices.DetectorIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.Detector];
            indices.DetectorIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Detector + 1];
            indices.SpeciesModifierIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.SpeciesModifier];
            indices.SpeciesModifierIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.SpeciesModifier + 1];
            indices.PortalIndexBegin = TypeIndex[(int)Manipulator.ManipulatorType.Portal];
            indices.PortalIndexEnd = TypeIndex[(int)Manipulator.ManipulatorType.Portal + 1];

            if (ConstDataID.Count != 0)
            {
                ConstDataID.Clear();
            }

#if ZIBRA_LIQUID_PAID_VERSION
            SDFTextureSize = 0;
            EmbeddingTextureSize = 0;
            TextureCount = 0;
            foreach (var manipulator in manipulators)
            {
                if (manipulator == null)
                    continue;

                if (manipulator.GetComponent<NeuralSDF>() != null)
                {
                    AddTexture(manipulator.GetComponent<NeuralSDF>());
                }
            }

            CalculateTextureData();
#endif
        }
    }
}
