using UnityEngine;

namespace Qin.UnityEffects.Fluids
{
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class TopDownWater2D : MonoBehaviour
    {
        public enum Basin { BoatWake, IslandCurrent, StreamConfluence, ForkedChannel, DrainVortex, RainPond }

        public const int Columns = 128, Rows = 72;
        public const float StepSeconds = 1f / 120f;
        [SerializeField] ComputeShader _solver;
        [SerializeField] Basin _profile;
        [SerializeField, Range(0, 1.4f)] float _drive = 1;
        [SerializeField, Range(.8f, 2f)] float _waveSpeed = 1.45f;
        [SerializeField, Range(.1f, 1.8f)] float _drag = .66f;
        [SerializeField] bool _automatic = true;

        public Basin Profile => _profile;
        public RenderTexture Field => _read;
        public float Elapsed => _steps * StepSeconds;
        public bool Automatic { get => _automatic; set => _automatic = value; }
        public bool InputEnabled { get; set; } = true;
        static readonly int BasinId = Shader.PropertyToID("_BasinProfile");
        static readonly int StateId = Shader.PropertyToID("_QinTopDownState");
        static readonly int ActorId = Shader.PropertyToID("_QinTopDownActor");
        MaterialPropertyBlock _properties;
        MeshRenderer _surface;
        RenderTexture _read, _write;
        int _clear, _step, _inject, _steps;
        double _remainder;
        Vector4 _actor;
        bool _hasInteractor;

        void OnEnable()
        {
            if (!_solver || !SystemInfo.supportsComputeShaders ||
                !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBFloat)) return;
            _surface = GetComponent<MeshRenderer>();
            _properties ??= new MaterialPropertyBlock();
            _clear = _solver.FindKernel("ClearGrid");
            _step = _solver.FindKernel("StepGrid");
            _inject = _solver.FindKernel("AddImpulse");
            _read = CreateField();
            _write = CreateField();
            ResetWater();
        }

        static RenderTexture CreateField()
        {
            RenderTexture texture = new(Columns, Rows, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
            {
                name = "Top-down water field", enableRandomWrite = true,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        void Update()
        {
            if (_automatic) Advance(Time.deltaTime);
        }

        // Position and velocity are in basin metres. The complete quad spans 8 by 4.5 metres.
        public void SetBoat(Vector2 position, Vector2 velocity)
        {
            _hasInteractor = true;
            _actor = new Vector4(position.x, position.y, velocity.x, velocity.y);
        }

        public void UseDemoBoat() { _hasInteractor = false; }

        public void Impulse(Vector2 position, float height, float radius)
        {
            if (!_read) return;
            _solver.SetInt("_Profile", (int)_profile);
            _solver.SetVector("_Impulse", new Vector4(position.x, position.y, Mathf.Clamp(height, -.3f, .3f),
                                                       Mathf.Clamp(radius, .0625f, 1f)));
            _solver.SetTexture(_inject, "_Write", _read);
            _solver.Dispatch(_inject, Columns / 8, Rows / 8, 1);
        }

        public void ResetWater()
        {
            if (!_read) return;
            _remainder = 0;
            _steps = 0;
            _solver.SetTexture(_clear, "_Write", _read);
            _solver.Dispatch(_clear, Columns / 8, Rows / 8, 1);
            _solver.SetTexture(_clear, "_Write", _write);
            _solver.Dispatch(_clear, Columns / 8, Rows / 8, 1);
            UpdateBoat();
            BindSurface();
        }

        public void Advance(float seconds)
        {
            if (!_read || !(seconds > 0)) return;
            _remainder += Mathf.Min(seconds, .20f);
            while (_remainder + 1e-8 >= StepSeconds)
            {
                _remainder -= StepSeconds;
                _steps++;
                UpdateBoat();
                _solver.SetInt("_Profile", (int)_profile);
                _solver.SetFloat("_Elapsed", Elapsed);
                _solver.SetFloat("_TimeStep", StepSeconds);
                _solver.SetFloat("_Drive", InputEnabled ? _drive : 0);
                _solver.SetFloat("_WaveSpeed", _waveSpeed);
                _solver.SetFloat("_Drag", _drag);
                _solver.SetVector("_Actor", _actor);
                _solver.SetTexture(_step, "_Read", _read);
                _solver.SetTexture(_step, "_Write", _write);
                _solver.Dispatch(_step, Columns / 8, Rows / 8, 1);
                (_read, _write) = (_write, _read);
            }
            BindSurface();
        }

        void UpdateBoat()
        {
            if (_hasInteractor) return;
            // The vessel turns outside the frame, so the visible pass has no position seam.
            float angle = Elapsed * .28f - 1.15f;
            float x = 5.3f * Mathf.Sin(angle);
            float vx = 1.484f * Mathf.Cos(angle);
            _actor = new Vector4(x, -.12f + .34f * Mathf.Sin(.91f * x), vx,
                                .34f * .91f * Mathf.Cos(.91f * x) * vx);
        }

        void BindSurface()
        {
            _surface.GetPropertyBlock(_properties);
            _properties.SetTexture(StateId, _read);
            _properties.SetVector(ActorId, _actor);
            _properties.SetFloat(BasinId, (int)_profile);
            _surface.SetPropertyBlock(_properties);
        }

        void OnDisable()
        {
            if (_surface && _properties != null)
            {
                _surface.GetPropertyBlock(_properties);
                _properties.SetTexture(StateId, Texture2D.blackTexture);
                _surface.SetPropertyBlock(_properties);
            }
            Release(ref _read);
            Release(ref _write);
        }

        static void Release(ref RenderTexture field)
        {
            if (!field) return;
            field.Release();
            if (Application.isPlaying) Destroy(field);
            else DestroyImmediate(field);
            field = null;
        }
    }
}
