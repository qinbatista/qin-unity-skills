using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReplayBuilder
{
    public static void Export()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("UI replay").AddComponent<IPhoneReplayPlayer>();
        EditorSceneManager.SaveScene(scene, "Assets/Replay.unity");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Replay.unity" },
            locationPathName = Environment.GetEnvironmentVariable("UI_REPLAY_EXPORT"),
            target = BuildTarget.iOS
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(report.summary.result.ToString());
    }
}
