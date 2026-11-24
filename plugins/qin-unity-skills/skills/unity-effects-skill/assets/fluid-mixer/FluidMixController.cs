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
        private float _seconds;
        private float _stoppedAt;
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
                Sample(_seconds + Time.deltaTime);
        }

        private void OnDisable()
        {
            Recycle();
            _simulation?.Dispose();
            _simulation = null;
        }

        private void Sample(float seconds)
        {
            _seconds = Mathf.Max(_seconds, seconds);
            float end = _stoppedAt < 0f ? _recipe.Duration : Mathf.Min(_recipe.Duration, _stoppedAt + FadeSeconds);
            if (_seconds >= end)
            {
                Recycle();
                return;
            }

            float baseFade = 1f - Mathf.SmoothStep(0f, 1f, (_seconds - _recipe.Duration + FadeSeconds) / FadeSeconds);
            float interruptFade = _stoppedAt < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (_seconds - _stoppedAt) / FadeSeconds);
            float weight = Mathf.SmoothStep(0f, 1f, _seconds / .12f) * Mathf.Min(baseFade, interruptFade);
            Vector3 center = _obstacleAnchor ? transform.InverseTransformPoint(_obstacleAnchor.position) : _recipe.ObstacleCenter;
            Vector4 obstacle = new Vector4(center.x, center.y, center.z, _recipe.SolidObstacle ? _recipe.ObstacleRadius : 0f);
            float admission = _stoppedAt < 0f ? _recipe.FeedDuration : Mathf.Min(_stoppedAt, _recipe.FeedDuration);
            _simulation.Advance(_seconds, admission, obstacle, _recipe.Pressure);
            if (_recipe.SurfaceWaves)
                BindField(_surface, _simulation.Waves, 0f, weight);
            if (_recipe.Feed != SOFluidMixRecipe.Inlet.SurfaceImpulse)
                BindField(_volume, _simulation.Film, 1f, weight);
            if (_sprayStarted)
                _spray.SetFloat(ReleaseWeight, weight);
            CurrentStage = StageAt(end);
        }

        private VisualStage StageAt(float end)
        {
            if (_stoppedAt >= 0f || _seconds >= end - FadeSeconds) return VisualStage.Retracting;
            if (_seconds <= 0f) return VisualStage.Prepare;
            if (_seconds < .065f) return VisualStage.Preparing;
            if (_seconds < .12f) return VisualStage.Prepared;
            return _seconds < _recipe.FeedDuration ? VisualStage.Releasing : VisualStage.Released;
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

        public void BeginCast(uint seed)
        {
            Recycle();
            if (!_recipe || !_program || !_surface || !_volume || !_spray || !FluidMixSimulation.Supported())
                return;
            _block ??= new MaterialPropertyBlock();
            _simulation ??= new FluidMixSimulation(_program);
            _simulation.Reset(_recipe, seed);
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
            if (CurrentStage == VisualStage.Recycled) return;
            _lastSampleFrame = Time.frameCount;
            Sample(seconds);
        }

        public void EndCast()
        {
            if (CurrentStage != VisualStage.Recycled && _stoppedAt < 0f) _stoppedAt = _seconds;
        }
    }
}
