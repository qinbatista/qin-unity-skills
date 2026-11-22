using System;
using System.Collections;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class GroundTravelCases
    {
        const float ContactHeight = 0.00001f;
        const float MaxPlantDrift = 0.003f;
        const float MaxHeadSecondDifference = 0.025f;
        enum Motion { Steady, ChangeSpeed, ReverseOnPlatform, Blocked }
        sealed class Observations
        {
            public int Steps, AtTwoSeconds, FlightFrames;
            public float Travel, Drift, HeadSecondDifference, MinimumBend = 1f;
            public float TurnHeadStep, TurnHipStep, TurnShoulderStep, TurnKneeStep, TurnSoleStep;
        }

        [UnityTest]
        public IEnumerator RenderedWalkAndRunKeepTheirFootprintsAtFullAndHalfSpeed()
        {
            yield return new EnterPlayMode();
            using EditorCaseAssets assets = new EditorCaseAssets();
            foreach (bool running in new[] { false, true })
            {
                AnimationClip clip = VirtualWalker.SaveGait(assets.Folder, running);
                float fullSpeed = running ? 3f : 2f;
                foreach (int fps in new[] { 30, 60 })
                {
                    Observations full = Observe(clip, Motion.Steady, fullSpeed, fps);
                    Observations half = Observe(clip, Motion.Steady, fullSpeed * 0.5f, fps);
                    Assert.That(full.Steps, Is.EqualTo(running ? 12 : 8));
                    Assert.That(half.Steps * 2, Is.EqualTo(full.Steps));
                    Assert.That(full.AtTwoSeconds, Is.EqualTo(half.Steps), "Equal distance must give equal observed landings.");
                    Assert.That(full.Travel, Is.EqualTo(fullSpeed * 4f).Within(0.001f));
                    Assert.That(half.Travel, Is.EqualTo(full.Travel * 0.5f).Within(0.001f));
                    foreach (Observations result in new[] { full, half })
                    {
                        Assert.That(result.Drift, Is.LessThan(MaxPlantDrift));
                        Assert.That(result.HeadSecondDifference, Is.LessThan(MaxHeadSecondDifference));
                        Assert.That(result.MinimumBend, Is.GreaterThan(0.02f));
                        if (running) Assert.That(result.FlightFrames, Is.Positive, "The authored run has a flight interval.");
                        else Assert.That(result.FlightFrames, Is.Zero);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator SpeedChangeBrakingHeldTurnAndBlockedPlatformRespectTheSupport()
        {
            yield return new EnterPlayMode();
            using EditorCaseAssets assets = new EditorCaseAssets();
            AnimationClip clip = VirtualWalker.SaveGait(assets.Folder, false);
            foreach (int fps in new[] { 30, 60 })
            {
                Observations change = Observe(clip, Motion.ChangeSpeed, 2f, fps);
                Assert.That(change.Steps, Is.EqualTo(6));
                Assert.That(change.Travel, Is.EqualTo(6f).Within(0.001f));
                Observations turn = Observe(clip, Motion.ReverseOnPlatform, 2f, fps);
                Assert.That(turn.Steps, Is.EqualTo(4));
                Assert.That(turn.Travel, Is.EqualTo(4f).Within(0.001f));
                Assert.That(turn.MinimumBend, Is.GreaterThan(0.02f));
                Assert.That(turn.TurnSoleStep, Is.LessThan(MaxPlantDrift));
                Assert.That(turn.TurnHeadStep, Is.LessThan(0.10f));
                Assert.That(turn.TurnHipStep, Is.LessThan(0.10f));
                Assert.That(turn.TurnShoulderStep, Is.LessThan(0.15f));
                Assert.That(turn.TurnKneeStep, Is.LessThan(0.17f));
                Observations blocked = Observe(clip, Motion.Blocked, 0f, fps);
                Assert.That(blocked.Travel, Is.Zero);
                Assert.That(blocked.Steps, Is.Zero, "A stationary rider must accumulate no gait phase.");
                foreach (Observations result in new[] { change, turn, blocked })
                {
                    Assert.That(result.Drift, Is.LessThan(MaxPlantDrift));
                    Assert.That(result.HeadSecondDifference, Is.LessThan(MaxHeadSecondDifference));
                }
            }
        }

        [UnityTest]
        public IEnumerator ObserverRejectsIndependentCadenceAndHeadDefects()
        {
            yield return new EnterPlayMode();
            using EditorCaseAssets assets = new EditorCaseAssets();
            AnimationClip clip = VirtualWalker.SaveGait(assets.Folder, false);
            foreach (int fps in new[] { 30, 60 })
            {
                Observations wrongRate = Observe(clip, Motion.Steady, 1f, fps, phaseFactor: 2f);
                Assert.That(wrongRate.Steps, Is.EqualTo(8), "Half-speed travel with a full-rate clip oversteps.");
                Assert.That(wrongRate.Drift, Is.GreaterThan(0.20f));
                Assert.That(wrongRate.HeadSecondDifference, Is.LessThan(MaxHeadSecondDifference));
                Observations headPop = Observe(clip, Motion.Steady, 1f, fps, wobble: true);
                Assert.That(headPop.Steps, Is.EqualTo(4));
                Assert.That(headPop.Drift, Is.LessThan(MaxPlantDrift));
                Assert.That(headPop.HeadSecondDifference, Is.GreaterThan(0.15f));
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        // The position motor publishes at 50 Hz. Render poses use the interpolated position
        // and its measured distance, including the change in heading on a translating support.
        static float[] Motor(Motion mode, float speed)
        {
            const int hz = 50;
            float[] positions = new float[4 * hz + 2];
            for (int tick = 1; tick < positions.Length; tick++)
            {
                float t = (tick - 0.5f) / hz;
                float velocity = speed;
                if (mode == Motion.ChangeSpeed && t >= 2f) velocity *= 0.5f;
                if (mode == Motion.ReverseOnPlatform)
                {
                    float legTime = t < 2f ? t : t - 2f;
                    float ramp = Mathf.Clamp01(Mathf.Min(legTime, 1.4f - legTime) / 0.4f);
                    velocity = (t < 2f ? speed : -speed) * ramp;
                }
                positions[tick] = positions[tick - 1] + velocity / hz;
            }
            return positions;
        }

        static Observations Observe(AnimationClip clip, Motion mode, float speed, int fps, float phaseFactor = 1f, bool wobble = false)
        {
            Transform support = VirtualWalker.Node("Support", null, Vector3.zero);
            try
            {
                using VirtualWalker actor = new VirtualWalker(support, clip);
                string scenario = FormattableString.Invariant($"{clip.name}-{mode}-{speed:0.##}-{fps}-{phaseFactor:0.#}-{(wobble ? "pop" : "clean")}");
                string evidence = Environment.GetEnvironmentVariable("CHARGE_PULSE_EVIDENCE");
                string folder = string.IsNullOrEmpty(evidence) ? null : Path.Combine(evidence, "ground-travel");
                if (folder != null) Directory.CreateDirectory(folder);
                bool render = mode == Motion.Steady || mode == Motion.ReverseOnPlatform;
                string output = folder == null ? null : Path.Combine(folder, scenario);
                using GaitCamera camera = render ? new GaitCamera(support, output, clip.name == "Run" ? 12f : 8f) : null;
                using StreamWriter trace = folder == null ? null : File.CreateText(Path.Combine(folder, scenario + ".csv"));
                trace?.WriteLine("t,root_x,left_x,left_y,left_z,right_x,right_y,right_z,head_y,landings");
                float[] fixedPosition = Motor(mode, speed);
                Observations result = new Observations();
                Vector3[] footprints = new Vector3[2];
                bool[] wasDown = new bool[2];
                Vector3[] previous = new Vector3[5];
                Vector3[] landmarks = new Vector3[5];
                Vector3 pivot = Vector3.zero;
                float previousX = 0f, lastHeadY = 0f, lastDy = 0f;
                double phase = 0.25;
                for (int frame = 0; frame <= 4 * fps; frame++)
                {
                    float t = (float)frame / fps;
                    float tick = t * 50f;
                    int index = Mathf.FloorToInt(tick);
                    float x = Mathf.Lerp(fixedPosition[index], fixedPosition[index + 1], tick - index);
                    float travel = Mathf.Abs(x - previousX);
                    result.Travel += travel;
                    phase += travel * phaseFactor / VirtualWalker.Stride;
                    previousX = x;
                    bool platform = mode == Motion.ReverseOnPlatform || mode == Motion.Blocked;
                    support.position = platform ? new Vector3(0.24f * t, 0.07f * Mathf.Sin(2f * t), 0f) : Vector3.zero;
                    float turn = mode == Motion.ReverseOnPlatform ? Mathf.SmoothStep(0f, 180f, Mathf.InverseLerp(1.6f, 2f, t)) : 0f;
                    Quaternion facing = Quaternion.Euler(0f, turn, 0f);
                    actor.Root.localRotation = facing;
                    actor.Root.localPosition = Vector3.right * x + pivot - facing * pivot;
                    actor.Sample(phase, wobble ? (frame % 2 == 0 ? -0.06f : 0.06f) : 0f);
                    if (mode == Motion.ReverseOnPlatform && t >= 1.4f && t < 1.6f)
                        pivot = actor.Root.InverseTransformPoint(actor.Soles[0].position);

                    Vector3 left = support.InverseTransformPoint(actor.Soles[0].position);
                    Vector3 right = support.InverseTransformPoint(actor.Soles[1].position);
                    int contacts = 0;
                    for (int side = 0; side < 2; side++)
                    {
                        Vector3 point = side == 0 ? left : right;
                        Assert.That(point.y, Is.GreaterThanOrEqualTo(-ContactHeight), "A shoe penetrated the ground.");
                        bool down = point.y <= ContactHeight;
                        if (down)
                        {
                            contacts++;
                            if (!wasDown[side])
                            {
                                footprints[side] = point;
                                if (frame > 0) result.Steps++;
                            }
                            result.Drift = Mathf.Max(result.Drift, Vector3.Distance(footprints[side], point));
                        }
                        wasDown[side] = down;
                        Vector3 knee = actor.Root.InverseTransformPoint(actor.Knees[side].position);
                        Vector3 ankle = actor.Root.InverseTransformPoint(actor.Soles[side].position) + new Vector3(-0.04f, 0.11f, 0f);
                        Vector3 hip = new Vector3(0f, actor.Hips.localPosition.y, ankle.z);
                        float bend = Vector3.Cross(ankle - hip, knee - hip).z / (ankle - hip).magnitude;
                        result.MinimumBend = Mathf.Min(result.MinimumBend, bend);
                    }
                    if (contacts == 0) result.FlightFrames++;
                    if (frame == 2 * fps) result.AtTwoSeconds = result.Steps;
                    float headY = support.InverseTransformPoint(actor.Head.position).y;
                    float dy = headY - lastHeadY;
                    if (frame >= 2) result.HeadSecondDifference = Mathf.Max(result.HeadSecondDifference, Mathf.Abs(dy - lastDy));
                    lastDy = dy;
                    lastHeadY = headY;
                    landmarks[0] = actor.Head.position;
                    landmarks[1] = actor.Hips.position;
                    landmarks[2] = actor.Shoulders[0].position;
                    landmarks[3] = actor.Knees[0].position;
                    landmarks[4] = actor.Soles[0].position;
                    for (int i = 0; i < landmarks.Length; i++) landmarks[i] = support.InverseTransformPoint(landmarks[i]);
                    if (mode == Motion.ReverseOnPlatform && t > 1.6f && t <= 2f)
                    {
                        result.TurnHeadStep = Mathf.Max(result.TurnHeadStep, Vector3.Distance(previous[0], landmarks[0]));
                        result.TurnHipStep = Mathf.Max(result.TurnHipStep, Vector3.Distance(previous[1], landmarks[1]));
                        result.TurnShoulderStep = Mathf.Max(result.TurnShoulderStep, Vector3.Distance(previous[2], landmarks[2]));
                        result.TurnKneeStep = Mathf.Max(result.TurnKneeStep, Vector3.Distance(previous[3], landmarks[3]));
                        result.TurnSoleStep = Mathf.Max(result.TurnSoleStep, Vector3.Distance(previous[4], landmarks[4]));
                    }
                    Array.Copy(landmarks, previous, landmarks.Length);
                    trace?.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9}", t, x, left.x, left.y, left.z, right.x, right.y, right.z, headY, result.Steps));
                    if (frame < fps * 4) camera?.Capture(frame);
                }
                TestContext.WriteLine(FormattableString.Invariant($"{scenario} travel={result.Travel:F4} landings={result.Steps} twoSeconds={result.AtTwoSeconds} drift={result.Drift:F6} headSecondDiff={result.HeadSecondDifference:F6} flightFrames={result.FlightFrames} kneeBend={result.MinimumBend:F4} turnSole={result.TurnSoleStep:F6}"));
                return result;
            }
            finally
            {
                Object.DestroyImmediate(support.gameObject);
            }
        }
    }
}
