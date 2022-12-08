using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace com.zibra.liquid.Manipulators
{
#if ZIBRA_LIQUID_PAID_VERSION
    public class ForceInteractionData
    {
        public Vector3 Force;
        public Vector3 Torque;
    }

    [System.Serializable]
    public class ForceInteractionCallbackType : UnityEvent<ForceInteractionData> { };
#endif

    public class ZibraLiquidCollider : Manipulator
    {
        // Store all colliders separately from all manipulators
        public static readonly List<ZibraLiquidCollider> AllColliders = new List<ZibraLiquidCollider>();

        [FormerlySerializedAs("FluidFriction")]
        [Tooltip(
            "0.0 fluid flows without friction, 1.0 fluid sticks to the surface (0 is hydrophobic, 1 is hydrophilic)")]
        [Range(0.0f, 1.0f)]
        public float Friction = 0.0f;

#if ZIBRA_LIQUID_PAID_VERSION
        [Tooltip("Allows the fluid to apply force to the object")]
        public bool ForceInteraction;

        [Tooltip(
            "Callback that is triggered before applying force interaction. Called even if force interaction is disabled, so you can get forces that would be applied to the object.")]
        public ForceInteractionCallbackType ForceInteractionCallback;
#endif

        override public ManipulatorType GetManipulatorType()
        {
#if ZIBRA_LIQUID_PAID_VERSION
            if (GetComponent<SDFObjects.NeuralSDF>() != null)
                return ManipulatorType.NeuralCollider;
            else
#endif
                return ManipulatorType.AnalyticCollider;
        }

        private void Update()
        {
            AdditionalData0.w = Friction;
        }

        public void ApplyForceTorque(Vector3 Force, Vector3 Torque)
        {
#if ZIBRA_LIQUID_PAID_VERSION
            ForceInteractionData forceInteractionData = new ForceInteractionData();
            forceInteractionData.Force = Force;
            forceInteractionData.Torque = Torque;

            if (ForceInteractionCallback != null)
            {
                ForceInteractionCallback.Invoke(forceInteractionData);
            }

            if (ForceInteraction)
            {
                Rigidbody rg = GetComponent<Rigidbody>();
                if (rg != null)
                {
                    rg.AddForce(forceInteractionData.Force, ForceMode.Force);
                    rg.AddTorque(forceInteractionData.Torque, ForceMode.Force);
                }
                else
                {
                    Debug.LogWarning(
                        "No rigid body component attached to collider, please add one for force interaction to work");
                }
            }
#endif
        }

#if UNITY_EDITOR
        override public Color GetGizmosColor()
        {
            switch (GetManipulatorType())
            {
#if ZIBRA_LIQUID_PAID_VERSION
            case Manipulator.ManipulatorType.NeuralCollider:
                return Color.grey;
#endif
            case Manipulator.ManipulatorType.AnalyticCollider:
            default:
                return new Color(0.2f, 0.9f, 0.9f);
            }
        }
#endif

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

        protected new void OnEnable()
        {
            if (!AllColliders?.Contains(this) ?? false)
            {
                AllColliders.Add(this);
            }
        }

        protected new void OnDisable()
        {
            if (AllColliders?.Contains(this) ?? false)
            {
                AllColliders.Remove(this);
            }
        }

        public virtual ulong GetMemoryFootrpint()
        {
            return 0;
        }
    }
}
