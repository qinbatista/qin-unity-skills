using System.Collections;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class QinFluidReplay
{
    private static readonly string[] AuthoredInlets = { "RainOnPool", "LedgeDrip", "SpringFountain" };

    [UnityTest]
    public IEnumerator CastWaterVariant([ValueSource(nameof(AuthoredInlets))] string form)
    {
        return RecordWaterVariant(form, false);
    }

    private static readonly string[] Limits = { "WeightlessMass", "SingleLedgeDrop", "WideCloudburst" };

    [UnityTest]
    public IEnumerator ParameterLimits([ValueSource(nameof(Limits))] string form)
    {
        return RecordWaterVariant(form, form == "WeightlessMass");
    }

    private IEnumerator RecordWaterVariant(string form, bool compareGravity)
    {
        string root = ApplicationCapture(form);
        SetChannelStage(ChannelWater2D.Boundary.OpenRiver, null, form);
        var mixer = OpenVariant(form, Vector3.zero, $"Assets/GameWater/{form}/{form}.prefab");
        var recipe = mixer.Recipe;
        mixer.SetRecipe(recipe);
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), dry);
        var control = Object.Instantiate(recipe);
        applicationResources.Add(control);
        SerializedObject edit = new(control);
        if (compareGravity)
            edit.FindProperty("_gravity").floatValue = 8f;
        else
            edit.FindProperty("_responses").intValue &= ~(int)SOFluidMixRecipe.Response.SurfaceWaves;
        edit.ApplyModifiedPropertiesWithoutUndo();

        string controlMode = compareGravity ? "gravity-on" : "no-waves";
        foreach (string mode in new[] { "normal", controlMode, "alternate" })
        {
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            mixer.SetRecipe(mode == controlMode ? control : recipe);
            mixer.BeginCast(mode == "alternate" ? Seed ^ 0x9e3779b9u : Seed);
            int last = mode == "alternate" ? 45 : 113;
            for (int frame = 0; frame <= last; frame++)
            {
                float time = frame / (float)Fps;
                mixer.SetCastTime(time);
                yield return null;
                mixer.SetCastTime(time);
                if (mode == "normal" || frame == 45 || (compareGravity && frame == 24)
                    || (form == "SingleLedgeDrop" && frame % 3 == 0 && frame <= 45)) SaveFrame(folder, frame);
                if (frame <= 96 && frame % 3 == 0)
                    File.WriteAllBytes(Path.Combine(folder, $"state-{frame:D4}.bin"), State(mixer));
            }
            if (mode != "alternate")
            {
                Assert.That(mixer.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
                Assert.That(Capture(), Is.EqualTo(dry));
            }
        }
    }
}
