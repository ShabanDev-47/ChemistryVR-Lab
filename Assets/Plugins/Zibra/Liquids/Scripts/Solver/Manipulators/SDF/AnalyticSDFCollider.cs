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
    [ExecuteInEditMode]
    [Obsolete]
    public class AnalyticSDFCollider : ZibraLiquidCollider
    {
        /// <summary>
        /// Currently chosen type of SDF collider
        /// </summary>
        [SerializeField]
        private SDFObject.SDFType chosenSDFType = SDFObject.SDFType.Sphere;

        [SerializeField]
        private bool InvertSDF = false;

#if UNITY_EDITOR
        public void Awake()
        {
            ZibraLiquidCollider collider = gameObject.AddComponent<ZibraLiquidCollider>();
            AnalyticSDF sdf = gameObject.AddComponent<AnalyticSDF>();

            collider.Friction = Friction;
#if ZIBRA_LIQUID_PAID_VERSION
            collider.ForceInteraction = ForceInteraction;
#endif

            sdf.chosenSDFType = chosenSDFType;
            sdf.InvertSDF = InvertSDF;

            ZibraLiquid[] allLiquids = FindObjectsOfType<ZibraLiquid>();

            foreach (var liquid in allLiquids)
            {
                ZibraLiquidCollider oldCollider = liquid.HasGivenCollider(gameObject);
                if (oldCollider != null)
                {
                    liquid.RemoveCollider(oldCollider);
                    liquid.AddCollider(collider);
                }
            }

            DestroyImmediate(this);
        }
#endif
    }
}