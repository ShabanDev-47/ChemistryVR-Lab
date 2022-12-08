#if ZIBRA_LIQUID_PAID_VERSION

using com.zibra.liquid.DataStructures;
using com.zibra.liquid.Solver;
using com.zibra.liquid.Utilities;
using com.zibra.liquid;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.zibra.liquid.SDFObjects
{
    public static class MeshUtilities
    {
        public static Mesh GetMesh(GameObject obj)
        {
            Renderer currentRenderer = obj.GetComponent<Renderer>();

            if (currentRenderer == null || currentRenderer is MeshRenderer)
            {
                var MeshFilter = obj.GetComponent<MeshFilter>();

                if (MeshFilter == null)
                {
#if UNITY_EDITOR
                    string errorMessage = "MeshFilter absent. Generating SDF requires mesh available.";
                    EditorUtility.DisplayDialog("Zibra Liquid Mesh Error", errorMessage, "Ok");
                    Debug.LogError(errorMessage);
#endif
                    return null;
                }

                if (MeshFilter.sharedMesh == null)
                {
#if UNITY_EDITOR
                    string errorMessage = "No mesh found on this object. Generating SDF requires mesh available.";
                    EditorUtility.DisplayDialog("Zibra Liquid Mesh Error", errorMessage, "Ok");
                    Debug.LogError(errorMessage);
#endif
                    return null;
                }

                return MeshFilter.sharedMesh;
            }

#if UNITY_EDITOR
            {
                string errorMessage =
                    "Unsupported Renderer type. Only MeshRenderer is supported at the moment.";
                EditorUtility.DisplayDialog("Zibra Liquid Mesh Error", errorMessage, "Ok");
                Debug.LogError(errorMessage);
            }
#endif
            return null;
        }

        // remove vertices which are not used by the triangles
        static public Mesh ClearBlanks(Mesh mesh)
        {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;

            List<Vector3> newVertList = new List<Vector3>();

            List<int> oldVertNewID = new List<int>();
            oldVertNewID.AddRange(Enumerable.Repeat(-1, vertices.Length));

            List<int> trianglesList = triangles.ToList();

            for (int i = 0; i < triangles.Length; i++)
            {
                int vertID = triangles[i];

                if (oldVertNewID[vertID] == -1) // add vertex
                {
                    oldVertNewID[vertID] = newVertList.Count;
                    newVertList.Add(vertices[vertID]);
                }

                trianglesList[i] = oldVertNewID[vertID];
            }

            triangles = trianglesList.ToArray();
            vertices = newVertList.ToArray();
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            return mesh;
        }
    }

}

#endif