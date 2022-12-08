using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using com.zibra.liquid.Manipulators;

namespace com.zibra.liquid.SDFObjects
{
    public class SDFColliderCompare : Comparer<ZibraLiquidCollider>
    {
        // Compares manipulator type ID
        public override int Compare(ZibraLiquidCollider x, ZibraLiquidCollider y)
        {
            int result = x.GetManipulatorType().CompareTo(y.GetManipulatorType());
            if (result != 0)
            {
                return result;
            }
            return x.GetHashCode().CompareTo(y.GetHashCode());
        }
    }

    // SDF Collider template
    [ExecuteInEditMode] // Careful! This makes script execute in edit mode.
                        // Use "EditorApplication.isPlaying" for play mode only check.
                        // Encase this check and "using UnityEditor" in "#if UNITY_EDITOR" preprocessor directive to
                        // prevent build errors
    [DisallowMultipleComponent]
    public abstract class SDFObject : MonoBehaviour
    {
        /// <summary>
        /// Types of Analytical SDF's
        /// </summary>
        public enum SDFType
        {
            Sphere,
            Box,
            Capsule,
            Torus,
            Cylinder,
        }
        /// <summary>
        /// Currently chosen type of SDF collider
        /// </summary>
        public SDFType chosenSDFType = SDFType.Sphere;

        [Tooltip("Inverts collider so liquid can only exist inside.")]
        public bool InvertSDF = false;

        [Tooltip("How far is the SDF surface from the object surface")]
        public float SurfaceDistance = 0.0f;

        virtual public Vector3 GetBBoxSize()
        {
            return transform.localScale;
        }
    }
}
