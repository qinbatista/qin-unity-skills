using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;

// Builds only the independent probe receiver prepared under task Cache.
public static class SODeviceProbeBuilder
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("SO_DEVICE_BUILD_OUTPUT");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Supply the task-owned build output.");
        PlayerSettings.companyName = "QinSkillTests";
        PlayerSettings.productName = "SO Storage Probe";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.qin.unityskills.sodataprobe");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.High);
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.appleDeveloperTeamID = Environment.GetEnvironmentVariable("SO_DEVICE_TEAM");
        string profile = Environment.GetEnvironmentVariable("SO_DEVICE_PROFILE");
        PlayerSettings.iOS.appleEnableAutomaticSigning = string.IsNullOrEmpty(profile);
        PlayerSettings.iOS.iOSManualProvisioningProfileID = profile ?? "";
        PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Development;
        Directory.CreateDirectory("Assets/Resources");
        GameObject source = new GameObject("Replicated storage");
        source.AddComponent<NetworkObject>();
        source.AddComponent<SOUnityNetcodeStore>().SetScopeBeforeSpawn("prefab/default");
        PrefabUtility.SaveAsPrefabAsset(source, "Assets/Resources/SODeviceNetwork.prefab");
        UnityEngine.Object.DestroyImmediate(source);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Probe camera").AddComponent<Camera>();
        EditorSceneManager.SaveScene(scene, "Assets/Probe.unity");
        AssetDatabase.SaveAssets();
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Probe.unity" }, target = BuildTarget.iOS, locationPathName = output, options = BuildOptions.None });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Probe build failed: " + report.summary.result);
    }
}
