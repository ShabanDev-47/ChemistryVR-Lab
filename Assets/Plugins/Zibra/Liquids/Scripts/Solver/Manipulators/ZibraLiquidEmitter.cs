using System;
using UnityEngine;
using UnityEngine.Serialization;
using com.zibra.liquid.Solver;
using com.zibra.liquid.SDFObjects;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

namespace com.zibra.liquid.Manipulators
{
    [AddComponentMenu("Zibra/Zibra Liquid Emitter")]
    [DisallowMultipleComponent]
    public class ZibraLiquidEmitter : Manipulator
    {
#if ZIBRA_LIQUID_PAID_VERSION
        [NonSerialized]
        public long createdParticlesTotal = 0;
        [NonSerialized]
        public int createdParticlesPerFrame = 0;
#endif

        [Obsolete("ClampBehaviorType is deprecated.", true)]
        public enum ClampBehaviorType
        {
            DontClamp,
            Clamp
        }

        [NonSerialized]
        [Obsolete("ParticlesPerSec is deprecated. Use VolumePerSec instead.", true)]
        public float ParticlesPerSec;

        [SerializeField]
        [FormerlySerializedAs("ParticlesPerSec")]
        private float ParticlesPerSecOld;

        [Tooltip("Emitted volume per simulation time unit")]
        [Min(0.0f)]
        public float VolumePerSimTime = 0.125f;

        [NonSerialized]
        [Obsolete("VelocityMagnitude is deprecated. Use InitialVelocity instead.", true)]
        public float VelocityMagnitude;

        [SerializeField]
        [FormerlySerializedAs("VelocityMagnitude")]
        private float VelocityMagnitudeOld;

        [NonSerialized]
        [Obsolete("CustomEmitterTransform is deprecated. Modify emitter's transform directly instead.", true)]
        public Transform CustomEmitterTransform;

        [SerializeField]
        [FormerlySerializedAs("CustomEmitterTransform")]
        private Transform CustomEmitterTransformOld;

        [Tooltip("Initial velocity of newly created particles")]
        // Rotated with object
        // Used velocity will be equal to GetRotatedInitialVelocity
        public Vector3 InitialVelocity = new Vector3(0, 0, 0);

        [NonSerialized]
        [Obsolete("PositionClampBehavior is deprecated. Clamp position of emitter manually if you need to.", true)]
        public ClampBehaviorType PositionClampBehavior;

        [HideInInspector]
        [SerializeField]
        private int ObjectVersion = 1;

#if UNITY_EDITOR
        void OnSceneOpened(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            Debug.Log("Zibra Liquid Emitter format was updated. Please resave scene.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        [ExecuteInEditMode]
        public void Awake()
        {
#if UNITY_EDITOR
            bool updated = false;
#endif
            // If Emitter is in old format we need to parse old parameters and come up with equivalent new ones
            if (ObjectVersion == 1)
            {
                InitialVelocity = transform.rotation * new Vector3(VelocityMagnitudeOld, 0, 0);
                VelocityMagnitudeOld = 0;
                transform.rotation = Quaternion.identity;
                if (CustomEmitterTransformOld)
                {
                    transform.position = CustomEmitterTransformOld.position;
                    transform.rotation = CustomEmitterTransformOld.rotation;
                    CustomEmitterTransformOld = null;
                }

                ObjectVersion = 2;
#if UNITY_EDITOR
                updated = true;
#endif
            }
            // If Emitter is in old format we need to parse old parameters and come up with equivalent new ones
            if (ObjectVersion == 2)
            {
                UnityEngine.Object[] liquids = FindObjectsOfType(typeof(ZibraLiquid));

                foreach (ZibraLiquid liquid in liquids)
                {
                    if (liquid.HasManipulator(this))
                    {
                        float cellSize = liquid.CellSize;
                        VolumePerSimTime = ParticlesPerSecOld * cellSize * cellSize * cellSize /
                                           liquid.solverParameters.ParticleDensity / liquid.simTimePerSec;
                        break;
                    }
                }

                ObjectVersion = 3;
#if UNITY_EDITOR
                updated = true;
#endif
            }

            // If Emitter is in old format we need to parse old parameters and come up with equivalent new ones
            if (ObjectVersion == 3)
            {
                if (GetComponent<SDFObject>() == null)
                {
                    AnalyticSDF sdf = gameObject.AddComponent<AnalyticSDF>();
                    sdf.chosenSDFType = SDFObject.SDFType.Box;
#if UNITY_EDITOR
                    updated = true;
#endif
                }

                ObjectVersion = 4;
            }

#if UNITY_EDITOR
            if (updated)
            {
                // Can't mark object dirty in Awake, since scene is not fully loaded yet
                UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            }
#endif
        }

        override public ManipulatorType GetManipulatorType()
        {
            return ManipulatorType.Emitter;
        }

        public Vector3 GetRotatedInitialVelocity()
        {
            return transform.rotation * InitialVelocity;
        }

        private void Update()
        {
            Vector3 rotatedInitialVelocity = GetRotatedInitialVelocity();
            AdditionalData0.y = rotatedInitialVelocity.x;
            AdditionalData0.z = rotatedInitialVelocity.y;
            AdditionalData0.w = rotatedInitialVelocity.z;
        }

        override public Matrix4x4 GetTransform()
        {
            return transform.localToWorldMatrix;
        }

        override public Quaternion GetRotation()
        {
            return transform.rotation;
        }

        override public Vector3 GetPosition()
        {
            return transform.position;
        }
        override public Vector3 GetScale()
        {
            return transform.lossyScale;
        }

#if UNITY_EDITOR
        override public Color GetGizmosColor()
        {
            return new Color(0.2f, 0.2f, 0.8f);
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled)
            {
                return;
            }

            Gizmos.color = Handles.color = GetGizmosColor();

            if (InitialVelocity.sqrMagnitude > Vector3.kEpsilon)
            {
                Utilities.GizmosHelper.DrawArrow(transform.position, GetRotatedInitialVelocity(), 0.5f);
            }
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }

        void Reset()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            ObjectVersion = 4;
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

        public new void OnDestroy()
        {
            base.OnDestroy();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        }
#endif
    }
}