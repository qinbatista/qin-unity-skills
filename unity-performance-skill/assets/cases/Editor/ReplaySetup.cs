using System;
using System.IO;
using UnityEditor;
using UnityEngine;
#if QIN_HAS_VFX
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Qin.UnityPerformance.Cases
{
    public static class ReplaySetup
    {
        public static void Configure()
        {
            if (!File.Exists(".qin-performance-replay"))
                throw new InvalidOperationException("Configure only the isolated project created by replay_cases.py.");

#if QIN_HAS_ADDRESSABLES
            if (Environment.GetEnvironmentVariable("QIN_PERFORMANCE_LOADING") == "1")
                AddressableCases.Configure();
#endif
#if QIN_HAS_VFX
            if (Environment.GetEnvironmentVariable("QIN_PERFORMANCE_GRAPHICS") == "1")
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                UniversalRendererData renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/PerformanceRenderer.asset");
                UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                AssetDatabase.CreateAsset(pipeline, "Assets/PerformancePipeline.asset");
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                AssetDatabase.SaveAssets();
                if (Environment.GetEnvironmentVariable("QIN_PERFORMANCE_GRAPH") == "1")
                    RenderingCases.Configure();
            }
#endif
            Debug.Log("QIN_PERFORMANCE_REPLAY_READY " + Application.unityVersion);
        }
    }
}
