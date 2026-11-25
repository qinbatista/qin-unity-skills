using System.Collections;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed partial class QinFluidReplay
{
    [UnityTest]
    public IEnumerator WideCloudburstProbe()
    {
        const string subject = "WideCloudburst";
        string root = ApplicationCapture(subject);
        SetChannelStage(ChannelWater2D.Boundary.OpenRiver, null, subject);
        FluidMixController mixer = OpenVariant(subject, Vector3.zero, $"Assets/GameWater/{subject}/{subject}.prefab");
        SOFluidMixRecipe authored = mixer.Recipe;
        Assert.That(authored.Feed, Is.EqualTo(SOFluidMixRecipe.Inlet.FallingDrops));
        Assert.That(authored.DropCount, Is.EqualTo(160));
        Assert.That(authored.DropSpan, Is.EqualTo(5f));
        mixer.SetRecipe(authored);
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), dry);

        foreach (string mode in new[] { "normal", "no-waves", "legacy" })
        {
            SOFluidMixRecipe recipe = Object.Instantiate(authored);
            applicationResources.Add(recipe);
            SerializedObject edit = new(recipe);
            if (mode == "no-waves") edit.FindProperty("_responses").intValue &= ~(int)SOFluidMixRecipe.Response.SurfaceWaves;
            if (mode == "legacy") edit.FindProperty("_rainExposure").floatValue = 0f;
            edit.ApplyModifiedPropertiesWithoutUndo();
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            mixer.SetRecipe(recipe);
            mixer.BeginCast(Seed);
            for (int frame = 0; frame < 114; frame++)
            {
                float time = frame / (float)Fps;
                mixer.SetCastTime(time);
                yield return null;
                mixer.SetCastTime(time);
                SaveFrame(folder, frame);
                if (mixer.CurrentStage != FluidMixController.VisualStage.Recycled)
                    File.WriteAllBytes(Path.Combine(folder, $"state-{frame:D4}.bin"), State(mixer));
                // The normal and no-wave shaders use the same persistent contact atlas.
                if (frame == 104 && mode != "legacy") WriteCloudburstContacts(mixer, folder);
            }
            Assert.That(mixer.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
            Assert.That(Capture(), Is.EqualTo(dry), "The side-view drawing survived cast retirement.");
        }
    }

    private static void WriteCloudburstContacts(FluidMixController owner, string folder)
    {
        object simulation = Simulation.GetValue(owner);
        RenderTexture atlas = (RenderTexture)simulation.GetType().GetProperty("Spray", Members).GetValue(simulation);
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(atlas, 0, TextureFormat.RGBAFloat);
        request.WaitForCompletion();
        Assert.That(request.hasError, Is.False);
        var texels = request.GetData<Vector4>();
        using StreamWriter output = new(Path.Combine(folder, "contacts.csv"));
        output.WriteLine("parcel,xMetres,surfaceYMetres,impactSeconds,downwardMetresPerSecond");
        for (int id = 0; id < owner.Recipe.DropCount; id++)
        {
            Vector4 hit = texels[atlas.width + id];
            if (hit.w > 0f) output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1:R},{2:R},{3:R},{4:R}", id, hit.x, hit.y, hit.z, hit.w));
        }
    }
}
