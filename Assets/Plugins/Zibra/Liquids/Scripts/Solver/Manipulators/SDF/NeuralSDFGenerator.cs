#if ZIBRA_LIQUID_PAID_VERSION && UNITY_EDITOR

using com.zibra.liquid.DataStructures;
using com.zibra.liquid.SDFObjects;
using System;
using com.zibra.liquid.Utilities;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace com.zibra.liquid.Editor.SDFObjects
{
    public abstract class NeuralSDFGenerator
    {
        // Limits for representation generation web requests
        protected const uint REQUEST_TRIANGLE_COUNT_LIMIT = 100000;
        protected const uint REQUEST_SIZE_LIMIT = 3 << 20; // 3mb

        protected Mesh meshToProcess;
        protected Bounds MeshBounds;
        protected UnityWebRequest CurrentRequest;

        public abstract void Start();

        protected bool CheckMeshSize()
        {
            if (meshToProcess.triangles.Length / 3 > REQUEST_TRIANGLE_COUNT_LIMIT)
            {
                string errorMessage =
                    $"Mesh is too large. Can't generate representation. Triangle count should not exceed {REQUEST_TRIANGLE_COUNT_LIMIT} triangles, but current mesh have {meshToProcess.triangles.Length / 3} triangles";
                EditorUtility.DisplayDialog("ZibraLiquid Error.", errorMessage, "OK");
                Debug.LogError(errorMessage);
                return true;
            }
            return false;
        }

        protected void SendRequest(string requestURL, string json)
        {
            if (CurrentRequest != null)
            {
                CurrentRequest.Dispose();
                CurrentRequest = null;
            }

            if (json.Length > REQUEST_SIZE_LIMIT)
            {
                string errorMessage =
                    $"Mesh is too large. Can't generate representation. Please decrease vertex/triangle count. Web request should not exceed {REQUEST_SIZE_LIMIT / (1 << 20):N2}mb, but for current mesh {(float)json.Length / (1 << 20):N2}mb is needed.";
                EditorUtility.DisplayDialog("ZibraLiquid Error.", errorMessage, "OK");
                Debug.LogError(errorMessage);
                return;
            }

            if (ZibraServerAuthenticationManager.GetInstance().GetStatus() ==
                ZibraServerAuthenticationManager.Status.OK)
            {
                if (requestURL != "")
                {
                    CurrentRequest = UnityWebRequest.Post(requestURL, json);
                    CurrentRequest.SendWebRequest();
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Zibra Liquid Error",
                                            ZibraServerAuthenticationManager.GetInstance().ErrorText, "Ok");
                Debug.LogError(ZibraServerAuthenticationManager.GetInstance().ErrorText);
            }
        }

        public void Abort()
        {
            CurrentRequest?.Dispose();
        }

        protected abstract void ProcessResult();

        public void Update()
        {
            if (CurrentRequest != null && CurrentRequest.isDone)
            {
#if UNITY_2020_2_OR_NEWER
                if (CurrentRequest.isDone && CurrentRequest.result == UnityWebRequest.Result.Success)
#else
                if (CurrentRequest.isDone && !CurrentRequest.isHttpError && !CurrentRequest.isNetworkError)
#endif
                {
                    ProcessResult();
                }
                else
                {
                    EditorUtility.DisplayDialog("Zibra Liquid Server Error", CurrentRequest.error, "Ok");
                    Debug.LogError(CurrentRequest.downloadHandler.text);
                }

                CurrentRequest.Dispose();
                CurrentRequest = null;

                // make sure to mark the scene as changed
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
        }

        public bool IsFinished()
        {
            return CurrentRequest == null;
        }
    }

    public class MeshNeuralSDFGenerator : NeuralSDFGenerator
    {
        private NeuralSDFRepresentation NeuralSDFInstance;

        public MeshNeuralSDFGenerator(NeuralSDFRepresentation NeuralSDF, Mesh mesh)
        {
            this.meshToProcess = mesh;
            this.NeuralSDFInstance = NeuralSDF;
        }

        public NeuralSDFRepresentation GetSDF()
        {
            return NeuralSDFInstance;
        }

        public void CreateMeshBBCube()
        {
            MeshBounds = meshToProcess.bounds;
            NeuralSDFInstance.BoundingBoxCenter = MeshBounds.center;
            NeuralSDFInstance.BoundingBoxSize = MeshBounds.size;
        }

        public override void Start()
        {
            if (CheckMeshSize())
                return;

            var meshRepresentation =
                new MeshRepresentation { vertices = meshToProcess.vertices.Vector3ToString(),
                                         faces = meshToProcess.triangles.IntToString(),
                                         vox_dim = NeuralSDFRepresentation.DEFAULT_EMBEDDING_GRID_DIMENSION,
                                         sdf_dim = NeuralSDFRepresentation.DEFAULT_SDF_APPROX_DIMENSION,
                                         cutoff_weight = 0.1f,
                                         static_quantization = true };

            var json = JsonUtility.ToJson(meshRepresentation);
            string requestURL = ZibraServerAuthenticationManager.GetInstance().GenerationURL;

            SendRequest(requestURL, json);
        }

        protected override void ProcessResult()
        {
            var json = CurrentRequest.downloadHandler.text;
            VoxelRepresentation newRepresentation =
                JsonUtility.FromJson<MultiVoxelRepresentation>(json).meshes_data[0];

            if (string.IsNullOrEmpty(newRepresentation.embeds) || string.IsNullOrEmpty(newRepresentation.sd_grid))
            {
                EditorUtility.DisplayDialog("Zibra Liquid Server Error",
                                            "Server returned empty result. Contact Zibra Liquid support", "Ok");
                Debug.LogError("Server returned empty result. Contact Zibra Liquid support");

                return;
            }

            CreateMeshBBCube();

            NeuralSDFInstance.CurrentRepresentationV3 = newRepresentation;
            NeuralSDFInstance.CreateRepresentation(NeuralSDFRepresentation.DEFAULT_EMBEDDING_GRID_DIMENSION,
                                                   NeuralSDFRepresentation.DEFAULT_SDF_APPROX_DIMENSION);
        }
    }

    static public class GenerationQueue
    {
        static Queue<NeuralSDFGenerator> SDFsToGenerate = new Queue<NeuralSDFGenerator>();
        static Dictionary<MeshNeuralSDFGenerator, NeuralSDF> Generators =
            new Dictionary<MeshNeuralSDFGenerator, NeuralSDF>();

        static void Update()
        {
            if (SDFsToGenerate.Count == 0)
                Abort();

            SDFsToGenerate.Peek().Update();
            if (SDFsToGenerate.Peek().IsFinished())
            {
                RemoveFromQueue();
                if (SDFsToGenerate.Count > 0)
                {
                    SDFsToGenerate.Peek().Start();
                }
            }
        }

        static void RemoveFromQueue()
        {
            if (SDFsToGenerate.Peek() is MeshNeuralSDFGenerator)
                Generators.Remove(SDFsToGenerate.Peek() as MeshNeuralSDFGenerator);

            SDFsToGenerate.Dequeue();

            if (SDFsToGenerate.Count == 0)
            {
                EditorApplication.update -= Update;
            }
        }

        static public void AddToQueue(NeuralSDFGenerator generator)
        {
            if (!SDFsToGenerate.Contains(generator))
            {
                if (SDFsToGenerate.Count == 0)
                {
                    EditorApplication.update += Update;
                    generator.Start();
                }
                SDFsToGenerate.Enqueue(generator);
            }
        }

        static public void AddToQueue(NeuralSDF sdf)
        {
            if (Contains(sdf))
                return;

            Mesh objectMesh = MeshUtilities.GetMesh(sdf.gameObject);
            if (objectMesh == null)
                return;

            MeshNeuralSDFGenerator gen = new MeshNeuralSDFGenerator(sdf.objectRepresentation, objectMesh);
            AddToQueue(gen);
            Generators[gen] = sdf;
        }

        static public void Abort()
        {
            if (SDFsToGenerate.Count > 0)
            {
                SDFsToGenerate.Peek().Abort();
                SDFsToGenerate.Clear();
                Generators.Clear();
                EditorApplication.update -= Update;
            }
        }

        static public int GetQueueLength()
        {
            return SDFsToGenerate.Count;
        }

        static public bool Contains(NeuralSDF sdf)
        {
            return Generators.ContainsValue(sdf);
        }
    }
}

#endif