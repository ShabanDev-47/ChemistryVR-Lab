#if ZIBRA_LIQUID_PAID_VERSION

using System;
using UnityEngine;
using com.zibra.liquid.Utilities;
using com.zibra.liquid.Manipulators;
using com.zibra.liquid.DataStructures;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.zibra.liquid.SDFObjects
{
    [Serializable]
    public class NeuralSDFRepresentation
    {
        // TODO implement variable resolution
        public const int BLOCK_SDF_APPROX_DIMENSION = 32;
        public const int BLOCK_EMBEDDING_GRID_DIMENSION = 21;

        public const int DEFAULT_SDF_APPROX_DIMENSION = 32;
        public const int DEFAULT_EMBEDDING_GRID_DIMENSION = 21;

        public const int PACKING = 4;
        public const int EMBEDDING_BASE_SIZE = 16;
        public const int EMBEDDING_SIZE = EMBEDDING_BASE_SIZE / PACKING;

        [SerializeField]
        public Vector3 BoundingBoxCenter;
        [SerializeField]
        public Vector3 BoundingBoxSize;

        public Matrix4x4 ObjectTransform;

        public VoxelRepresentation CurrentRepresentationV3 = new VoxelRepresentation();
        [HideInInspector]
        public bool HasRepresentationV3;

        [SerializeField]
        public VoxelEmbedding VoxelInfo;

        [SerializeField]
        public int EmbeddingResolution;

        [SerializeField]
        public int GridResolution;

        [SerializeField]
        public bool HasHash = false;

        [SerializeField]
        public ZibraHash128 ObjectHash;

        public byte GetSDGrid(int i, int j, int k, int t)
        {
            int id = i + GridResolution * (j + k * GridResolution);

            return VoxelInfo.grid[2 * id + t];
        }

        public Color32 GetEmbedding(int i, int j, int k, int t)
        {
            int id = i + t * EmbeddingResolution + EMBEDDING_SIZE * EmbeddingResolution * (j + k * EmbeddingResolution);
            return VoxelInfo.embeds[id];
        }

        public ZibraHash128 GetHash()
        {
            if (!HasHash)
            {
                ObjectHash = new ZibraHash128();
                ObjectHash.Init();
                ObjectHash.Append(VoxelInfo.embeds);

                HasHash = true;
            }
            return ObjectHash;
        }

        public void CreateRepresentation(int embeddingResolution, int gridResolution)
        {
            HasRepresentationV3 = true;

            var embeds = CurrentRepresentationV3.embeds.StringToBytes();
            VoxelInfo.grid = CurrentRepresentationV3.sd_grid.StringToBytes();

            EmbeddingResolution = embeddingResolution;
            GridResolution = gridResolution;

            int embeddingSize = embeddingResolution * embeddingResolution * embeddingResolution;

            Array.Resize<Color32>(ref VoxelInfo.embeds, embeddingSize * EMBEDDING_SIZE);

            for (int i = 0; i < embeddingResolution; i++)
            {
                for (int j = 0; j < embeddingResolution; j++)
                {
                    for (int k = 0; k < embeddingResolution; k++)
                    {
                        for (int t = 0; t < EMBEDDING_SIZE; t++)
                        {
                            int id0 = i + t * embeddingResolution +
                                      EMBEDDING_SIZE * embeddingResolution * (j + k * embeddingResolution);
                            int id1 = t + (i + embeddingResolution * (j + k * embeddingResolution)) * EMBEDDING_SIZE;
                            Color32 embeddings = new Color32(embeds[PACKING * id1 + 0], embeds[PACKING * id1 + 1],
                                                             embeds[PACKING * id1 + 2], embeds[PACKING * id1 + 3]);
                            VoxelInfo.embeds[id0] = embeddings;
                        }
                    }
                }
            }

            CurrentRepresentationV3.embeds = null;
            CurrentRepresentationV3.sd_grid = null;

            float[] Q = CurrentRepresentationV3.transform.Q.StringToFloat();
            float[] T = CurrentRepresentationV3.transform.T.StringToFloat();
            float[] S = CurrentRepresentationV3.transform.S.StringToFloat();

            Quaternion Rotation = (new Quaternion(-Q[1], -Q[2], -Q[3], Q[0]));
            Vector3 Scale = new Vector3(S[0], S[1], S[2]);
            Vector3 Translation = new Vector3(-T[0], -T[1], -T[2]);

            ObjectTransform = Matrix4x4.Rotate(Rotation) * Matrix4x4.Translate(Translation) * Matrix4x4.Scale(Scale);
        }
    }

    [ExecuteInEditMode] // Careful! This makes script execute in edit mode.
    // Use "EditorApplication.isPlaying" for play mode only check.
    // Encase this check and "using UnityEditor" in "#if UNITY_EDITOR" preprocessor directive to prevent build errors
    [AddComponentMenu("Zibra/Zibra Neural SDF")]
    public class NeuralSDF : SDFObject
    {
        [SerializeField]
        public NeuralSDFRepresentation objectRepresentation = new NeuralSDFRepresentation();

        public bool HasRepresentation()
        {
            return objectRepresentation != null && objectRepresentation.HasRepresentationV3;
        }

#if UNITY_EDITOR
        public void OnDrawGizmosSelected()
        {
            Manipulator manip = GetComponent<Manipulator>();

            if (manip == null || !manip.enabled)
            {
                return;
            }

            Gizmos.color = Handles.color = manip.GetGizmosColor();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(objectRepresentation.BoundingBoxCenter, objectRepresentation.BoundingBoxSize);
        }

        public void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }
#endif
    }
}

#endif