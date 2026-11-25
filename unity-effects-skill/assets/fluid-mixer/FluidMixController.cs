using UnityEngine;
using UnityEngine.VFX;

namespace Qin.UnityEffects.Fluids
{
    [DisallowMultipleComponent]
    public sealed class FluidMixController : MonoBehaviour
    {
        public enum VisualStage { Prepare, Preparing, Prepared, Releasing, Released, Retracting, Recycled }

        private const float FadeSeconds = .28f;
        private static readonly int Field = Shader.PropertyToID("_LiquidFilm");
        private static readonly int Side = Shader.PropertyToID("_NearSide");
        private static readonly int FilmWeight = Shader.PropertyToID("_FilmWeight");
        private static readonly int MaterialState = Shader.PropertyToID("MaterialState");
        private static readonly int ReleaseWeight = Shader.PropertyToID("ReleaseWeight");

        [SerializeField] private MeshRenderer _surface;
        [SerializeField] private MeshRenderer _volume;
        [SerializeField] private VisualEffect _spray;
        [SerializeField] private ComputeShader _program;
        [SerializeField] private SOFluidMixRecipe _recipe;
        [SerializeField] private Transform _obstacleAnchor;

        [Tooltip("Optional sorting layer for all three draws. For 2D refraction, select a layer above the camera colour capture.")]
        [SerializeField] private string _waterSortingLayer;

        private FluidMixSimulation _simulation;
        private MaterialPropertyBlock _block;
        private double _seconds;
        private double _stoppedAt;
        private int _lastSampleFrame;
        private bool _sprayStarted;

        public VisualStage CurrentStage { get; private set; } = VisualStage.Recycled;
        public SOFluidMixRecipe Recipe => _recipe;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_waterSortingLayer))
                SetWaterSortingLayer(_waterSortingLayer);
            if (_recipe && _recipe.PlayOnEnable)
                BeginCast(unchecked((uint)GetEntityId().GetHashCode()));
        }

        private void LateUpdate()
        {
            if (CurrentStage != VisualStage.Recycled && _lastSampleFrame != Time.frameCount)
                Sample(_recipe.ContinuousFeed ? _seconds + Time.deltaTime : (float)_seconds + Time.deltaTime);
        }

        private void OnDisable()
        {
            Recycle();
            _simulation?.Dispose();
            _simulation = null;
        }

        private void Sample(double seconds)
        {
            _seconds = System.Math.Max(_seconds, seconds);
            bool continuous = _recipe.ContinuousFeed;
            double naturalEnd = continuous ? double.PositiveInfinity : _recipe.Duration;
            double end = naturalEnd;
            if (_stoppedAt >= 0)
            {
                double drainedAt = continuous ? _stoppedAt + FluidMixSimulation.ContinuousDrainSeconds
                    : (float)_stoppedAt + FadeSeconds;
                end = System.Math.Min(naturalEnd, drainedAt);
            }
            if (_seconds >= end)
            {
                Recycle();
                return;
            }

            float castTime = (float)_seconds;
            float baseFade = continuous ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (castTime - _recipe.Duration + FadeSeconds) / FadeSeconds);
            float interruptFade = 1f;
            if (_stoppedAt >= 0)
            {
                float fadeAge = continuous ? (float)(_seconds - end) + FadeSeconds : castTime - (float)_stoppedAt;
                interruptFade = 1f - Mathf.SmoothStep(0f, 1f, fadeAge / FadeSeconds);
            }
            float weight = Mathf.SmoothStep(0f, 1f, castTime / .12f) * Mathf.Min(baseFade, interruptFade);
            Vector3 center = _obstacleAnchor ? transform.InverseTransformPoint(_obstacleAnchor.position) : _recipe.ObstacleCenter;
            Vector4 obstacle = new Vector4(center.x, center.y, center.z, _recipe.SolidObstacle ? _recipe.ObstacleRadius : 0f);
            double feedEnd = continuous ? double.PositiveInfinity : _recipe.FeedDuration;
            double admission = _stoppedAt < 0 ? feedEnd : System.Math.Min(_stoppedAt, feedEnd);
            _simulation.Advance(_seconds, admission, obstacle, _recipe.Pressure);
            if (_recipe.SurfaceWaves)
                BindField(_surface, _simulation.Waterline, 0f, weight);
            if (_recipe.Feed != SOFluidMixRecipe.Inlet.SurfaceImpulse)
                BindField(_volume, _simulation.Film, 1f, weight);
            if (_sprayStarted)
                _spray.SetFloat(ReleaseWeight, weight);
            CurrentStage = StageAt((float)end);
        }

        private VisualStage StageAt(float end)
        {
            if (_stoppedAt >= 0f || _seconds >= end - FadeSeconds) return VisualStage.Retracting;
            if (_seconds <= 0f) return VisualStage.Prepare;
            if (_seconds < .065f) return VisualStage.Preparing;
            if (_seconds < .12f) return VisualStage.Prepared;
            return _recipe.ContinuousFeed || _seconds < _recipe.FeedDuration ? VisualStage.Releasing : VisualStage.Released;
        }

        private void BindField(MeshRenderer draw, RenderTexture texture, float side, float weight)
        {
            _block.SetTexture(Field, texture);
            _block.SetFloat(Side, side);
            _block.SetFloat(FilmWeight, weight);
            draw.SetPropertyBlock(_block);
            draw.enabled = true;
        }

        private void Recycle()
        {
            if (_sprayStarted && _spray)
            {
                _spray.SetFloat(ReleaseWeight, 0f);
                _spray.Stop();
                _spray.Reinit();
                _spray.ResetOverride(MaterialState);
                _sprayStarted = false;
            }
            if (_surface) { _surface.enabled = false; _surface.SetPropertyBlock(null); }
            if (_volume) { _volume.enabled = false; _volume.SetPropertyBlock(null); }
            CurrentStage = VisualStage.Recycled;
        }

        public void SetRecipe(SOFluidMixRecipe recipe)
        {
            Recycle();
            _recipe = recipe;
        }

        public void SetWaterSortingLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return;
            foreach (SortingLayer layer in SortingLayer.layers)
            {
                if (layer.name != layerName) continue;
                _surface.sortingLayerID = layer.id;
                _volume.sortingLayerID = layer.id;
                _spray.GetComponent<Renderer>().sortingLayerID = layer.id;
                _waterSortingLayer = layerName;
                return;
            }
            Debug.LogWarning($"Unknown fluid sorting layer '{layerName}'. Choose an existing project layer.", this);
        }

        public void SetObstacleAnchor(Transform anchor)
        {
            _obstacleAnchor = anchor;
        }

        public void BeginCast(uint seed) => BeginChargedCast(seed, 1f);

        public void BeginChargedCast(uint seed, float charge)
        {
            Recycle();
            if (!_recipe || !_program || !_surface || !_volume || !_spray || !float.IsFinite(charge) || !FluidMixSimulation.Supported())
                return;
            _block ??= new MaterialPropertyBlock();
            _simulation ??= new FluidMixSimulation(_program);
            _simulation.Reset(_recipe, seed, _recipe.Strength * Mathf.Clamp01(charge));
            _seconds = 0f;
            _stoppedAt = -1f;
            _lastSampleFrame = Time.frameCount;
            _sprayStarted = _recipe.Spray && _recipe.Feed != SOFluidMixRecipe.Inlet.SurfaceImpulse;
            if (_sprayStarted)
            {
                _spray.resetSeedOnPlay = false;
                _spray.startSeed = seed;
                _spray.SetTexture(MaterialState, _simulation.Spray);
                _spray.SetFloat(ReleaseWeight, 0f);
                _spray.Play();
            }
            Sample(0f);
        }

        public void SetCastTime(float seconds)
        {
            if (CurrentStage == VisualStage.Recycled || !float.IsFinite(seconds)) return;
            _lastSampleFrame = Time.frameCount;
            Sample(seconds);
        }

        public void EndCast()
        {
            if (CurrentStage != VisualStage.Recycled && _stoppedAt < 0f) _stoppedAt = _seconds;
        }
    }
}
