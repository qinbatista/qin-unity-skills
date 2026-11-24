using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace Qin.UnityEffects.Fluids
{
    [DisallowMultipleComponent]
    public sealed class ChannelWater2D : MonoBehaviour
    {
        public enum Boundary { OpenRiver, SlopingShore, ClosedBasin }

        private const int Cells = 128;
        private const int Hertz = 150;
        private const float Fade = .42f;
        private static readonly int Read = Shader.PropertyToID("_Read");
        private static readonly int Write = Shader.PropertyToID("_Write");
        private static readonly int Presentation = Shader.PropertyToID("_Presentation");
        private static readonly int Clock = Shader.PropertyToID("_Clock");
        private static readonly int StartWeight = Shader.PropertyToID("_StartWeight");
        private static readonly int Kick = Shader.PropertyToID("_Kick");
        private static readonly int Field = Shader.PropertyToID("_LiquidFilm");
        private static readonly int Side = Shader.PropertyToID("_NearSide");
        private static readonly int Weight = Shader.PropertyToID("_FilmWeight");
        private static readonly int MaterialState = Shader.PropertyToID("MaterialState");
        private static readonly int ReleaseWeight = Shader.PropertyToID("ReleaseWeight");
        private static readonly int MoteCount = Shader.PropertyToID("MoteCount");

        [SerializeField] private ComputeShader _program;
        [SerializeField] private MeshRenderer _surface;
        [SerializeField] private VisualEffect _foam;
        [SerializeField] private Boundary _boundary;
        [SerializeField, Range(.25f, .78f)] private float _poolDepth = .53f;
        [SerializeField, Range(0f, 1.55f)] private float _bankRise = .22f;
        [SerializeField, Range(0f, 1.4f)] private float _drive = 1f;
        [SerializeField, Range(2.2f, 6f)] private float _period = 3.9f;
        [SerializeField] private int _seed = 29411;
        [SerializeField] private string _waterSortingLayer;

        private ComputeShader _solver;
        private readonly RenderTexture[] _state = new RenderTexture[2];
        private RenderTexture _presentation;
        private CommandBuffer _commands;
        private MaterialPropertyBlock _properties;
        private int _initialize, _integrate, _present, _current;
        private long _tick;
        private double _elapsed, _ending;
        private float _currentDrive, _targetDrive, _strength, _cycle;
        private bool _running, _externallyTimed;

        public bool IsRunning => _running;
        public Boundary Profile => _boundary;

        private void OnEnable()
        {
            BeginFlow(unchecked((uint)_seed));
        }

        private void LateUpdate()
        {
            if (_running && !_externallyTimed)
                AdvanceTo(_elapsed + Mathf.Min(Time.deltaTime, .1f));
        }

        private void OnDisable()
        {
            HideWater();
            ReleaseResources();
        }

        private void ReleaseResources()
        {
            if (_commands != null) _commands.Release();
            _commands = null;
            for (int i = 0; i < _state.Length; i++)
            {
                Release(_state[i]);
                _state[i] = null;
            }
            Release(_presentation);
            _presentation = null;
            if (_solver) DestroyOwned(_solver);
            _solver = null;
        }

        public void BeginFlow(uint seed)
        {
            if (!isActiveAndEnabled || !_surface || !_program || !_foam || !SystemInfo.supportsComputeShaders
                || !SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, GraphicsFormatUsage.LoadStore))
                return;
            EnsureResources();
            _strength = Mathf.Clamp(_drive, 0f, 1.4f);
            _cycle = Mathf.Clamp(_period, 2.2f, 6f);
            _solver.SetInt("_Boundary", (int)_boundary);
            _solver.SetFloat("_Drive", _strength);
            _solver.SetFloat("_PoolDepth", Mathf.Clamp(_poolDepth, .25f, .78f));
            _solver.SetFloat("_BankRise", Mathf.Clamp(_bankRise, 0f, 1.55f));
            _solver.SetFloat("_Phase", (seed * 2654435761u >> 8) * (2f * Mathf.PI / 16777216f));
            _solver.SetTexture(_initialize, Write, _state[0]);
            _solver.Dispatch(_initialize, Cells / 32, 1, 1);
            _current = 0;
            _tick = 0;
            _elapsed = 0;
            _ending = -1;
            _targetDrive = 1f;
            _currentDrive = 0f;
            _running = true;
            _externallyTimed = false;
            _foam.Reinit();
            _foam.resetSeedOnPlay = false;
            _foam.startSeed = seed;
            _foam.SetFloat(MoteCount, Cells);
            _foam.SetTexture(MaterialState, _presentation);
            _foam.SetFloat(ReleaseWeight, 0f);
            _foam.Play();
            ApplySorting();
            RecordPresentation();
            AdvanceTo(0);
        }

        public void SetDriver(float weight)
        {
            _targetDrive = Mathf.Clamp01(weight);
        }

        public void EndFlow()
        {
            if (_running && _ending < 0)
            {
                _targetDrive = 0f;
                _ending = _elapsed;
            }
        }

        public void SetTime(double seconds)
        {
            if (!_running || !double.IsFinite(seconds)) return;
            _externallyTimed = true;
            AdvanceTo(seconds);
        }

        private void AdvanceTo(double seconds)
        {
            _elapsed = Math.Max(_elapsed, seconds);
            if (_ending >= 0 && _elapsed >= _ending + Fade)
            {
                HideWater();
                return;
            }
            long target = (long)Math.Floor(_elapsed * Hertz + .00001);
            if (target > _tick)
            {
                while (_tick < target)
                {
                    RecordStep(++_tick);
                    if ((_tick % Hertz) == 0) Execute();
                }
                RecordPresentation();
            }
            float fadeIn = Mathf.SmoothStep(0f, 1f, (float)_elapsed / Fade);
            float fadeOut = _ending < 0 ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (float)(_elapsed - _ending) / Fade);
            _properties.SetFloat(Weight, fadeIn * fadeOut);
            _surface.SetPropertyBlock(_properties);
            _surface.enabled = true;
            _foam.SetFloat(ReleaseWeight, fadeIn * fadeOut);
        }

        private void EnsureResources()
        {
            if (_solver) return;
            try
            {
                _solver = Instantiate(_program);
                _initialize = _solver.FindKernel("Initialize");
                _integrate = _solver.FindKernel("Integrate");
                _present = _solver.FindKernel("Present");
                _commands = new CommandBuffer { name = "Side-view shallow channel" };
                _state[0] = MakeTexture(1, "Channel state A");
                _state[1] = MakeTexture(1, "Channel state B");
                _presentation = MakeTexture(4, "Channel surface and advected foam");
                _properties = new MaterialPropertyBlock();
                _properties.SetTexture(Field, _presentation);
                _properties.SetFloat(Side, 2f);
            }
            catch
            {
                ReleaseResources();
                throw;
            }
        }

        private void RecordStep(long tick)
        {
            double seconds = tick / (double)Hertz;
            _currentDrive = Mathf.MoveTowards(_currentDrive, _targetDrive, 1f / (Hertz * .38f));
            _commands.SetComputeFloatParam(_solver, StartWeight, Mathf.SmoothStep(0, 1, _currentDrive));
            float phase = (float)(seconds % _cycle / _cycle) * 2f * Mathf.PI;
            _commands.SetComputeFloatParam(_solver, Clock, phase);
            // A basin receives one finite horizontal impulse. Reflected waves continue under their own momentum.
            float pulse = seconds < .60 ? Mathf.Sin(Mathf.PI * (float)seconds / .60f) : 0f;
            _commands.SetComputeFloatParam(_solver, Kick, _boundary == Boundary.ClosedBasin ?
                4.8f * _strength * _currentDrive * pulse * pulse : 0f);
            int next = 1 - _current;
            _commands.SetComputeTextureParam(_solver, _integrate, Read, _state[_current]);
            _commands.SetComputeTextureParam(_solver, _integrate, Write, _state[next]);
            _commands.DispatchCompute(_solver, _integrate, Cells / 32, 1, 1);
            _current = next;
        }

        private void RecordPresentation()
        {
            _commands.SetComputeTextureParam(_solver, _present, Read, _state[_current]);
            _commands.SetComputeTextureParam(_solver, _present, Presentation, _presentation);
            _commands.DispatchCompute(_solver, _present, Cells / 32, 1, 1);
            Execute();
        }

        private void Execute()
        {
            Graphics.ExecuteCommandBuffer(_commands);
            _commands.Clear();
        }

        private void ApplySorting()
        {
            if (string.IsNullOrEmpty(_waterSortingLayer)) return;
            _surface.sortingLayerName = _waterSortingLayer;
            _foam.GetComponent<Renderer>().sortingLayerName = _waterSortingLayer;
        }

        private void HideWater()
        {
            if (_running && _foam)
            {
                _foam.Stop();
                _foam.Reinit();
                _foam.ResetOverride(MaterialState);
            }
            if (_surface) { _surface.enabled = false; _surface.SetPropertyBlock(null); }
            _running = false;
        }

        private static RenderTexture MakeTexture(int rows, string label)
        {
            RenderTexture texture = new(Cells, rows, 0, GraphicsFormat.R32G32B32A32_SFloat)
            {
                name = label, enableRandomWrite = true, wrapMode = TextureWrapMode.Clamp,
                filterMode = rows == 1 ? FilterMode.Point : FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            if (texture.Create()) return texture;
            DestroyOwned(texture);
            throw new InvalidOperationException("Failed to allocate " + label);
        }

        private static void Release(RenderTexture texture)
        {
            if (!texture) return;
            texture.Release();
            DestroyOwned(texture);
        }

        private static void DestroyOwned(UnityEngine.Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
