using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_PIPELINE_URP
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Collections.Generic;
#endif

namespace com.zibra.liquid.Utilities
{
    public class RenderPipelineDetector
    {
        public enum RenderPipeline
        {
            SRP,
            URP,
            HDRP
        }
        public static RenderPipeline GetRenderPipelineType()
        {
            if (GraphicsSettings.currentRenderPipeline)
            {
                if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                {
#if !UNITY_PIPELINE_HDRP
                    Debug.LogError("Current detected render pipeline is HDRP, but UNITY_PIPELINE_HDRP is not defined");
#endif
                    return RenderPipeline.HDRP;
                }
                else
                {
#if !UNITY_PIPELINE_URP
                    Debug.LogError("Current detected render pipeline is URP, but UNITY_PIPELINE_URP is not defined");
#endif
                    return RenderPipeline.URP;
                }
            }
            else
            {
                return RenderPipeline.SRP;
            }
        }

        public static bool IsURPMissingRenderComponent()
        {
#if UNITY_PIPELINE_URP
            if (GetRenderPipelineType() == RenderPipeline.URP)
            {
                // Getting non public list of render features via reflection
                var URPAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
                var field = typeof(ScriptableRenderer)
                                .GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && URPAsset != null)
                {
                    var scriptableRendererFeatures =
                        field.GetValue(URPAsset.scriptableRenderer) as List<ScriptableRendererFeature>;
                    if (scriptableRendererFeatures != null)
                    {
                        foreach (var renderFeature in scriptableRendererFeatures)
                        {
                            if (renderFeature is LiquidURPRenderComponent)
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
            }
#endif
            return false;
        }

        public static bool IsURPMissingDepthBuffer()
        {
#if UNITY_PIPELINE_URP
            if (GetRenderPipelineType() == RenderPipeline.URP)
            {
                // Getting non public list of render features via reflection
                var URPAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
                return URPAsset != null && !URPAsset.supportsCameraDepthTexture;
            }
#endif
            return false;
        }
    }
}
