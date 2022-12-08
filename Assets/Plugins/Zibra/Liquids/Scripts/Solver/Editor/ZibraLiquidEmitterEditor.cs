using UnityEngine;
using UnityEditor;
using com.zibra.liquid.SDFObjects;
using com.zibra.liquid.Manipulators;

namespace com.zibra.liquid.Editor.Solver
{
    [CustomEditor(typeof(ZibraLiquidEmitter))]
    [CanEditMultipleObjects]
    public class ZibraLiquidEmitterEditor : ZibraLiquidManipulatorEditor
    {
        private ZibraLiquidEmitter[] EmitterInstances;

        private SerializedProperty VolumePerSimTime;
        private SerializedProperty InitialVelocity;
        public override void OnInspectorGUI()
        {
            bool missingSDF = false;
#if ZIBRA_LIQUID_PAID_VERSION
            bool hasNeuralSDF = false;
#endif

            foreach (var instance in EmitterInstances)
            {
                SDFObject sdf = instance.GetComponent<SDFObject>();
                if (sdf == null)
                {
                    missingSDF = true;
                    continue;
                }

#if ZIBRA_LIQUID_PAID_VERSION
                if (sdf is NeuralSDF)
                {
                    hasNeuralSDF = true;
                    continue;
                }
#endif
            }

            if (missingSDF)
            {
                if (EmitterInstances.Length > 1)
                    EditorGUILayout.HelpBox("At least 1 emitter missing shape. Please add Zibra SDF.",
                                            MessageType.Error);
                else
                    EditorGUILayout.HelpBox("Missing emitter shape. Please add Zibra SDF.", MessageType.Error);
                if (GUILayout.Button(EmitterInstances.Length > 1 ? "Add Analytic SDFs" : "Add Analytic SDF"))
                {
                    foreach (var instance in EmitterInstances)
                    {
                        if (instance.GetComponent<SDFObject>() == null)
                        {
                            Undo.AddComponent<AnalyticSDF>(instance.gameObject);
                        }
                    }
                }
                GUILayout.Space(5);
            }

#if ZIBRA_LIQUID_PAID_VERSION
            if (hasNeuralSDF)
            {
                if (EmitterInstances.Length > 1)
                    EditorGUILayout.HelpBox(
                        "At least 1 emitter has Neural SDF. Neural SDFs on Emitters are not supported in this version.",
                        MessageType.Error);
                else
                    EditorGUILayout.HelpBox("Neural SDFs on Emitters are not supported in this version",
                                            MessageType.Error);
                if (GUILayout.Button(EmitterInstances.Length > 1 ? "Replace Neural SDFs with Analytic SDFs"
                                                                 : "Replace Neural SDF with Analytic SDF"))
                {
                    foreach (var instance in EmitterInstances)
                    {
                        var sdf = instance.GetComponent<NeuralSDF>();
                        if (sdf != null)
                        {
                            Undo.RecordObject(instance.gameObject, "Added Analytic SDF");
                            DestroyImmediate(sdf);
                            Undo.AddComponent<AnalyticSDF>(instance.gameObject);
                        }
                    }
                }
                GUILayout.Space(5);
            }
#endif

#if ZIBRA_LIQUID_PAID_VERSION
            if (EmitterInstances.Length > 1)
                GUILayout.Label("Multiple emitters selected. Showing sum of all selected instances.");
            long createdTotal = 0;
            int createdCurrentFrame = 0;
            foreach (var instance in EmitterInstances)
            {
                createdTotal += instance.createdParticlesTotal;
                createdCurrentFrame += instance.createdParticlesPerFrame;
            }
            GUILayout.Label("Total amount of created particles: " + createdTotal);
            GUILayout.Label("Amount of created particles per frame: " + createdCurrentFrame);
            GUILayout.Space(10);
#endif

            serializedObject.Update();

            EditorGUILayout.PropertyField(VolumePerSimTime);
            EditorGUILayout.PropertyField(InitialVelocity);

            serializedObject.ApplyModifiedProperties();
        }

        // clang-format doesn't parse code with new keyword properly
        // clang-format off

        protected new void OnEnable()
        {
            base.OnEnable();

            EmitterInstances = new ZibraLiquidEmitter[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                EmitterInstances[i] = targets[i] as ZibraLiquidEmitter;
            }

            VolumePerSimTime = serializedObject.FindProperty("VolumePerSimTime");
            InitialVelocity = serializedObject.FindProperty("InitialVelocity");
        }
    }
}