using UnityEngine;

namespace Qin.UnityEffects
{
    // Serialized identifiers. Append new subjects after MeteorImpact.
    public enum FlowSky2DCase
    {
        Stream, ShoreWaves, Waterfall, CeilingDrips, RainPuddle, Whirlpool, RisingFlood, Wake,
        ParallaxStars, ShootingStars, Constellation, Aurora, SpiralGalaxy, Nebula, EclipseCorona, StarShower,
        WaterBolt, TidalSlash, Geyser, WaterShield, HealingSpring, StarLance, GravityWell, MeteorImpact
    }

    public enum FlowSky2DStage { Prepare, Preparing, Prepared, Releasing, Released, Retracting, Recycled }

    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class FlowSky2DPlayer : MonoBehaviour
    {
        private static readonly int CaseId = Shader.PropertyToID("_Case");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        [SerializeField] private FlowSky2DCase subject;
        [SerializeField, Min(0.1f)] private float loopSeconds = 4f;
        [SerializeField, Min(0.1f)] private float castSeconds = 2.4f;
        [SerializeField, Min(0.05f)] private float retractSeconds = 0.30f;
        [SerializeField] private bool playOnEnable = true;
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private float elapsed, stopElapsed, stopWeight, opacity, phase;
        private bool stopping;

        public FlowSky2DStage Stage { get; private set; } = FlowSky2DStage.Recycled;
        public bool AutomaticClock { get; set; } = true;
        public FlowSky2DCase Subject => subject;

        private void Awake()
        {
            surface = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play(subject);
            else
                Recycle();
        }

        private void Update()
        {
            if (AutomaticClock)
                Advance(Time.deltaTime);
        }

        private void OnDisable() => Recycle();

        // For a replacement with a visible tail, stop this instance and start a second pooled instance.
        public void Play(FlowSky2DCase selected)
        {
            subject = selected;
            elapsed = stopElapsed = phase = opacity = 0f;
            stopping = false;
            Stage = FlowSky2DStage.Prepare;
            surface.enabled = true;
            Apply();
        }

        public void Stop()
        {
            if (Stage == FlowSky2DStage.Recycled || stopping)
                return;
            stopping = true;
            stopWeight = opacity;
            stopElapsed = 0f;
            Stage = FlowSky2DStage.Retracting;
        }

        public void Advance(float seconds)
        {
            if (Stage == FlowSky2DStage.Recycled || seconds <= 0f)
                return;
            bool ambient = (int)subject < (int)FlowSky2DCase.WaterBolt;
            float period = Mathf.Max(0.1f, ambient ? loopSeconds : castSeconds);
            if (stopping)
            {
                stopElapsed += seconds;
                float drain = stopElapsed / Mathf.Max(0.05f, retractSeconds);
                if (drain >= 1f)
                {
                    Recycle();
                    return;
                }
                if (ambient)
                    phase = Mathf.Repeat(phase + seconds / period, 1f);
                opacity = stopWeight * (1f - Mathf.SmoothStep(0f, 1f, drain));
            }
            else
            {
                elapsed += seconds;
                float progress = elapsed / period;
                if (!ambient && progress >= 1f)
                {
                    Recycle();
                    return;
                }
                phase = ambient ? Mathf.Repeat(progress, 1f) : progress;
                opacity = Mathf.SmoothStep(0f, 1f, progress / 0.12f);
                if (!ambient && progress > 0.83f)
                    opacity *= 1f - Mathf.SmoothStep(0f, 1f, (progress - 0.83f) / 0.17f);
                if (progress < 0.12f) Stage = FlowSky2DStage.Preparing;
                else if (progress < 0.18f) Stage = FlowSky2DStage.Prepared;
                else if (progress < 0.42f) Stage = FlowSky2DStage.Releasing;
                else if (!ambient && progress > 0.83f) Stage = FlowSky2DStage.Retracting;
                else Stage = FlowSky2DStage.Released;
                // Keep the ambient clock bounded after the initial admission.
                if (ambient && elapsed > period * 2f)
                    elapsed = period + phase * period;
            }
            Apply();
        }

        private void Apply()
        {
            surface.GetPropertyBlock(properties);
            properties.SetFloat(CaseId, (int)subject);
            properties.SetFloat(PhaseId, phase);
            properties.SetFloat(OpacityId, opacity);
            surface.SetPropertyBlock(properties);
        }

        private void Recycle()
        {
            stopping = false;
            Stage = FlowSky2DStage.Recycled;
            if (surface != null)
                surface.enabled = false;
        }
    }
}
