using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class GraphTrialReplay
{
    public static void Configure()
    {
        if (!File.Exists(".qin-graph-trials"))
            throw new InvalidOperationException("Select a disposable graph trial project.");
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(renderer, "Assets/TrialRenderer.asset");
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        AssetDatabase.CreateAsset(pipeline, "Assets/TrialPipeline.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        AssetDatabase.SaveAssets();
        Debug.Log("QIN_GRAPH_TRIALS_READY");
    }
}
