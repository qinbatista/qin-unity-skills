using System.Collections;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class QinFluidReplay
{
    private static readonly string[] SpellProfiles =
    {
        "UpwellGeyser", "ChargedSurge", "MovingWardJet", "ShieldIntercept", "RecastRelay", "RadialBreak"
    };

    [UnityTest]
    public IEnumerator SpellMatrix([ValueSource(nameof(SpellProfiles))] string form)
    {
        string root = ApplicationCapture(form);
        bool receiver = false;
        BindReceiverStage(false, ref receiver);
        Vector3 ground = receiver ? Vector3.up * ApplicationFloor : Vector3.zero;
        if (!receiver) SetChannelStage(ChannelWater2D.Boundary.OpenRiver, null, form);
        if (!receiver && form == "UpwellGeyser")
        {
            // Preserve the ground registration while framing the upper spray and its full fall.
            const float framing = 1.30f;
            Transform scenery = stage.transform.Find("Channel side elevation");
            scenery.localScale *= framing;
            scenery.localPosition = new Vector3(0f, scenery.localPosition.y * framing, scenery.localPosition.z);
            camera.orthographicSize *= framing;
            camera.transform.position = new Vector3(0f, scenery.position.y, camera.transform.position.z);
        }
        FluidMixController water = OpenVariant(form, ground, $"Assets/GameWater/{form}/{form}.prefab");
        SOFluidMixRecipe recipe = water.Recipe;
        water.SetRecipe(recipe);
        Transform outlet = SpellFixtureDisc("Outlet", ground + recipe.Origin, .16f, false);
        outlet.gameObject.SetActive(!receiver);
        Vector3 anchor = ground + recipe.ObstacleCenter;
        Transform target = SpellFixtureDisc("Receiver", anchor, recipe.ObstacleRadius, true);
        target.gameObject.SetActive(recipe.SolidObstacle);
        Transform absent = new GameObject("Out of reach receiver").transform;
        absent.SetParent(stage.transform, false);
        absent.localPosition = Vector3.up * 9f;
        yield return null;
        Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), Capture());

        foreach (string mode in new[] { "normal", "control" })
        {
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            bool control = mode == "control";
            water.SetRecipe(recipe);
            water.transform.localScale = Vector3.one;
            outlet.position = ground + recipe.Origin;
            target.position = anchor;
            water.SetObstacleAnchor(control ? absent : target);
            int clockOrigin = 0;
            int cast = 1;
            float charge = control && (form == "ChargedSurge" || form == "RadialBreak") ? .20f : 1f;
            water.BeginChargedCast(Seed, charge);
            using var inputs = new StreamWriter(Path.Combine(folder, "input.csv"));
            inputs.WriteLine("frame,anchorX,anchorY,cast,stage,energy");
            for (int frame = 0; frame < 96; frame++)
            {
                float elapsed = frame / (float)Fps;
                if (form == "MovingWardJet")
                {
                    target.position = anchor + new Vector3(.49f * Mathf.Sin(4.15f * elapsed),
                        .38f * Mathf.Sin(5.1f * elapsed + .18f), 0f);
                }
                if (form == "RecastRelay" && frame == 14) water.EndCast();
                if (form == "RecastRelay" && frame == 30)
                {
                    Assert.That(water.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
                    // Mirror the whole XY component so the second world-space discharge faces left.
                    water.transform.localScale = new Vector3(-1f, 1f, 1f);
                    outlet.position = water.transform.TransformPoint(recipe.Origin);
                    if (!control)
                    {
                        clockOrigin = frame;
                        cast++;
                        water.BeginCast(Seed);
                    }
                }
                float time = (frame - clockOrigin) / (float)Fps;
                water.SetCastTime(time);
                yield return null;
                water.SetCastTime(time);
                if (!control || receiver || frame % 6 == 0 || frame == 95) SaveFrame(folder, frame);
                if (frame % 6 == 0)
                    File.WriteAllBytes(Path.Combine(folder, $"state-{frame:D4}.bin"), State(water));
                Vector3 localAnchor = water.transform.InverseTransformPoint(target.position);
                inputs.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:R},{2:R},{3},{4},{5:R}",
                    frame, localAnchor.x, localAnchor.y, cast, water.CurrentStage, charge));
            }
            Assert.That(water.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
            water.transform.localScale = Vector3.one;
            outlet.position = ground + recipe.Origin;
            target.position = anchor;
            var dry = File.ReadAllBytes(Path.Combine(root, "dry.png"));
            Assert.That(Capture(), Is.EqualTo(dry), "Spell retirement left visible water");
        }
    }

    private Transform SpellFixtureDisc(string name, Vector3 center, float radius, bool ward)
    {
        // A stationary brass nozzle or a driven ward makes the supplied physical boundary legible.
        const int resolution = 96;
        Color32[] texels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            Vector2 point = new((x + .5f) / resolution * 2f - 1f, (y + .5f) / resolution * 2f - 1f);
            float r = point.magnitude;
            if (r >= 1f) continue;
            float angle = Mathf.Atan2(point.y, point.x);
            float normalZ = Mathf.Sqrt(Mathf.Max(0f, 1f - r * r));
            float light = Mathf.Clamp01(.43f - .29f * point.x + .35f * point.y + .20f * normalZ);
            Color dark = new(.15f, .25f, .26f), bronze = new(.75f, .57f, .32f);
            float ring = Mathf.Clamp01(1f - 13f * Mathf.Abs(r - .76f));
            float studs = r > .67f && r < .85f ? Mathf.Pow(Mathf.Max(0, Mathf.Cos(8f * angle)), 9f) : 0f;
            var tint = Color.Lerp(dark, bronze, Mathf.Clamp01(.12f + .78f * light + .23f * ring + .18f * studs));
            if (!ward && r < .46f) tint = Color.Lerp(dark * .45f, dark, r);
            if (ward && r < .58f) tint = Color.Lerp(dark, new Color(.42f, .64f, .60f),
                light * .73f + .17f * Mathf.Cos(5f * angle + r * 3f));
            tint.a = Mathf.Clamp01((1f - r) * 44f);
            texels[y * resolution + x] = tint;
        }
        Texture2D texture = new(resolution, resolution, TextureFormat.RGBA32, false);
        texture.SetPixels32(texels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), Vector2.one * .5f,
            resolution / (2f * radius));
        SpriteRenderer renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        renderer.transform.SetParent(stage.transform, false);
        renderer.transform.position = center;
        renderer.sprite = sprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.sortingOrder = -2;
        applicationResources.Add(sprite);
        applicationResources.Add(texture);
        return renderer.transform;
    }
}
