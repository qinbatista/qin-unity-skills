using System;
using System.Collections.Generic;
using UnityEngine;

// A drop has one ballistic path and a single ground intersection. The same
// event drives the aerial streak, the short contact splash and the GPU field.
internal sealed class RainDropTimeline
{
    public const int Hz = 120;
    public const int Frames = 270;
    public const int Fps = 30;
    public const int Capacity = 64;
    public const float ShortWaveLifetime = 1.08f;
    public const int FirstContact = 108;
    public const float Gravity = 9.81f;
    // The fixed oblique view shows a horizontal window of the 8 m field.
    public const float ViewWidthM = 8f * .76f;
    const int SourceCutoff = 738;
    const float Shutter = 1f / 52f;

    internal readonly struct Drop
    {
        public readonly int Id, Land;
        public readonly float Birth, Duration, Strength, Radius, Speed, Wind;
        public readonly Vector2 UV;
        public Drop(int id, int land, float duration, float strength, float speed, float wind, Vector2 uv)
        {
            Id = id;
            Land = land;
            Duration = duration;
            Birth = land / (float)Hz - duration;
            Strength = strength;
            Speed = speed;
            Wind = wind;
            Radius = Mathf.Lerp(.070f, .116f, strength);
            UV = uv;
        }

        public float ImpactSpeed => Speed + Gravity * Duration;
        public float Amplitude => Mathf.Lerp(.006f, .020f, Strength);
        public Vector2 World => Vector2.Scale(UV - new Vector2(.5f, .5f), new Vector2(ViewWidthM, 4.5f));

        Vector2 Project(float time)
        {
            float remaining = Mathf.Clamp(Land / (float)Hz - time, 0, Duration);
            float height = remaining * (ImpactSpeed - .5f * Gravity * remaining);
            return UV + new Vector2(-Wind * remaining / ViewWidthM - height * .018f, height * .235f);
        }

        public Vector4 Streak(float time)
        {
            Vector2 tip = Project(time);
            Vector2 tail = Project(Mathf.Max(Birth, time - Shutter));
            return new Vector4(tip.x, tip.y, tail.x, tail.y);
        }
    }

    public readonly List<Drop> Drops = new();
    readonly System.Random _random;
    float Between(float a, float b) => Mathf.Lerp(a, b, (float)_random.NextDouble());

    public RainDropTimeline(int seed, Texture2D shoreMask)
    {
        _random = new System.Random(seed);
        Drops.Add(new Drop(0, FirstContact, .54f, 1f, 1.2f, .49f, new Vector2(.46f, .53f)));
        Drops.Add(new Drop(1, 205, .46f, .79f, 1.6f, .57f, new Vector2(.71f, .37f)));
        Drops.Add(new Drop(2, 237, .53f, .68f, 1.4f, .54f, new Vector2(.33f, .31f)));
        // Integrate the expected arrival rate on the physical source clock;
        // flight time naturally leaves a short landing tail after source cutoff.
        float quota = 0;
        for (int tick = 230; tick <= SourceCutoff; tick++)
        {
            float time = tick / (float)Hz;
            float ramp = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.1f, 4.0f, time));
            quota += Mathf.Lerp(3.5f, 49f, ramp) / Hz;
            if (quota < 1) continue;
            quota -= 1;
            float duration = Between(.34f, .62f);
            int land = tick + Mathf.RoundToInt(duration * Hz);
            duration = (land - tick) / (float)Hz;
            Vector2 position = SampleOpenWater(shoreMask);
            Drops.Add(new Drop(Drops.Count, land, duration, Between(.26f, 1f), Between(1.1f, 2.2f), Between(.39f, .70f), position));
        }
        Drops.Sort((a, b) => a.Land != b.Land ? a.Land.CompareTo(b.Land) : a.Id.CompareTo(b.Id));
    }

    Vector2 SampleOpenWater(Texture2D mask)
    {
        // Stay inside the rounded solver domain. The backdrop matte excludes
        // foreground boulders, including the ones above the water in this view.
        for (int attempt = 0; attempt < 256; attempt++)
        {
            Vector2 uv = new(Between(.055f, .945f), Between(.065f, .935f));
            Vector2 q = (uv - Vector2.one * .5f) * 2;
            q.x *= ViewWidthM / 8f;
            if (Mathf.Pow(Mathf.Abs(q.x), 8) + Mathf.Pow(Mathf.Abs(q.y), 8) > .32f) continue;
            if (mask.GetPixelBilinear(uv.x, uv.y).r > .99f) return uv;
        }
        throw new InvalidOperationException("The receiving matte has insufficient open water for a contact.");
    }

    public void Appearance(int tick, Vector4[] drops, Vector4[] hits, out int airborne, out int fresh)
    {
        airborne = 0;
        fresh = 0;
        float time = tick / (float)Hz;
        foreach (Drop drop in Drops)
        {
            float age = (tick - drop.Land) / (float)Hz;
            if (time >= drop.Birth && tick < drop.Land)
            {
                if (airborne >= drops.Length) throw new InvalidOperationException("Airborne rain exceeds the shader buffer.");
                drops[airborne++] = drop.Streak(time);
            }
            else if (age >= 0 && age <= ShortWaveLifetime)
            {
                if (fresh >= hits.Length) throw new InvalidOperationException("Recent rain contacts exceed the shader buffer.");
                hits[fresh++] = new Vector4(drop.UV.x, drop.UV.y, age, drop.Strength);
            }
        }
    }
}
