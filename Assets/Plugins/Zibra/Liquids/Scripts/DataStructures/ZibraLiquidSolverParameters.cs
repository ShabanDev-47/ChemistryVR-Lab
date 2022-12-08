using System;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace com.zibra.liquid.DataStructures
{
    [Serializable]
    public class ZibraLiquidSolverParameters : MonoBehaviour
    {
        public const float GRAVITY_THRESHOLD = 100f;
        public const float DEFAULT_STIFFNESS = 0.1f;
        public const float DEFAULT_DENSITY = 2.0f;
        public const float DEFAULT_MAX_VELOCITY = 3.0f;
        public const float DEFAULT_VISCOSITY = 0.392f;
        public const float DEFAULT_GRAVITY = -9.81f;
        public Vector3 Gravity = new Vector3(0.0f, DEFAULT_GRAVITY, 0.0f);

        [Tooltip("The stiffness of the liquid.")]
        [Min(0.02f)]
        public float FluidStiffness = DEFAULT_STIFFNESS;

        [Tooltip("The sharpness of the stiffness.")]
        [Range(1.0f, 8.0f)]
        [HideInInspector]
        public float FluidStiffnessPower = 3.0f;

        [NonSerialized]
        [HideInInspector]
        [Obsolete("ParticlesPerCell is deprecated. Use ParticleDensity instead.", true)]
        public float ParticlesPerCell;

        [Tooltip(
            "Resting density of particles. measured in particles/cell. This option directly affects volume of each particle. Higher values correspond to less volume, but higher quality simulation.")]
        [FormerlySerializedAs("ParticlesPerCell")]
        [Range(0.1f, 10.0f)]
        public float ParticleDensity = 2.0f;

        [Range(0.0f, 1.0f)]
        public float Viscosity = DEFAULT_VISCOSITY;

#if ZIBRA_LIQUID_PAID_VERSION
        [Tooltip("You can set this parameter to negative value to get a spaghettification effect")]
        [Range(-1.0f, 1.0f)]
        public float SurfaceTension = 0.0f;
#endif

        [NonSerialized]
        [HideInInspector]
        [Obsolete("VelocityLimit is deprecated. Use MaximumVelocity instead.", true)]
        public float VelocityLimit;

        [Tooltip("The velocity limit of the particles")]
        [FormerlySerializedAs("VelocityLimit")]
        [Range(0.0f, 10.0f)]
        public float MaximumVelocity = DEFAULT_MAX_VELOCITY;

#if ZIBRA_LIQUID_PAID_VERSION
        [Tooltip(
            "Minimum velocity the particles can have, non-zero values make an infinite flow. For normal liquid this value should be 0.")]
        [Range(0.0f, 10.0f)]
        public float MinimumVelocity = 0.0f;

        [Tooltip("The strength of the force acting on rigid bodies. Have exponential scale, from exp(-4) to exp(4).")]
        [Range(-1.0f, 1.0f)]
        public float ForceInteractionStrength = 0.0f;
#endif

#if UNITY_EDITOR
        public void OnValidate()
        {
            ValidateParameters();
        }
#endif

        public void ValidateParameters()
        {
            ValidateGravity(ref Gravity);
        }

        public void ValidateGravity(ref Vector3 gravity)
        {
            gravity.x = Mathf.Clamp(gravity.x, -GRAVITY_THRESHOLD, GRAVITY_THRESHOLD);
            gravity.y = Mathf.Clamp(gravity.y, -GRAVITY_THRESHOLD, GRAVITY_THRESHOLD);
            gravity.z = Mathf.Clamp(gravity.z, -GRAVITY_THRESHOLD, GRAVITY_THRESHOLD);
        }

        [System.Serializable]
        public class SolverSettings
        {
            public Vector3 Gravity = new Vector3(0.0f, -9.81f, 0.0f);

            [Tooltip("The stiffness of the liquid.")]
            [Min(0.02f)]
            public float FluidStiffness;

            [Tooltip(
                "Resting density of particles. measured in particles/cell. This option directly affects volume of each particle. Higher values correspond to less volume, but higher quality simulation.")]
            [Range(0.1f, 10.0f)]
            public float ParticleDensity;

            [Range(0.0f, 1.0f)]
            public float Viscosity;

#if ZIBRA_LIQUID_PAID_VERSION
            [Tooltip("You can set this parameter to negative value to get a spaghettification effect")]
            [Range(-1.0f, 1.0f)]
            public float SurfaceTension;
#endif

            [Tooltip("The velocity limit of the particles")]
            [Range(0.0f, 10.0f)]
            public float MaximumVelocity;
        }
    }
}