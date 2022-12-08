using com.zibra.liquid.Manipulators;
using UnityEditor;
using UnityEngine;

namespace com.zibra.liquid.Editor.Solver
{
    public class ZibraLiquidManipulatorEditor : UnityEditor.Editor
    {
        protected void TriggerRepaint()
        {
            Repaint();
        }

        protected void OnEnable()
        {
            Manipulator manipulator = target as Manipulator;
            manipulator.onChanged += TriggerRepaint;
        }

        protected void OnDisable()
        {
            Manipulator manipulator = target as Manipulator;
            manipulator.onChanged -= TriggerRepaint;
        }
    }
}
