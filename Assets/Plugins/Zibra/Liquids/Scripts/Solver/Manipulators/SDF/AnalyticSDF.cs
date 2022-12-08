using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using com.zibra.liquid.Solver;
using com.zibra.liquid.Manipulators;

namespace com.zibra.liquid.SDFObjects
{
    /// <summary>
    /// An analytical ZibraFluid SDF
    /// </summary>
    [AddComponentMenu("Zibra/Zibra Analytic SDF")]
    public class AnalyticSDF : SDFObject
    {
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Manipulator manip = GetComponent<Manipulator>();

            if (!isActiveAndEnabled || (manip != null && !manip.enabled))
            {
                return;
            }

            Color gizmosColor = manip == null ? Color.red : manip.GetGizmosColor();

            Gizmos.color = Handles.color = gizmosColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Handles.zTest = CompareFunction.LessEqual;
            switch (chosenSDFType)
            {
            case SDFType.Sphere:
                Gizmos.DrawWireSphere(new Vector3(0, 0, 0), 0.5f);
                break;
            case SDFType.Box:
                Gizmos.DrawWireCube(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
                break;
            case SDFType.Capsule:
                Utilities.GizmosHelper.DrawWireCapsule(transform.position, transform.rotation,
                                                       0.5f * transform.lossyScale.x, 0.5f * transform.lossyScale.y);
                break;
            case SDFType.Torus:
                Utilities.GizmosHelper.DrawWireTorus(transform.position, transform.rotation,
                                                     0.5f * transform.lossyScale.x, transform.lossyScale.y);
                break;
            case SDFType.Cylinder:
                Utilities.GizmosHelper.DrawWireCylinder(transform.position, transform.rotation,
                                                        0.5f * transform.lossyScale.x, transform.lossyScale.y);
                break;
            }
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }
#endif

        public override Vector3 GetBBoxSize()
        {
            Vector3 scale = transform.lossyScale;
            switch (chosenSDFType)
            {
            default:
                return 0.5f * scale;
            case SDFType.Capsule:
                return new Vector3(scale.x, scale.y, scale.x);
            case SDFType.Torus:
                return new Vector3(scale.x, scale.y, scale.x);
            case SDFType.Cylinder:
                return new Vector3(scale.x, scale.y, scale.x);
            }
        }
    }
}