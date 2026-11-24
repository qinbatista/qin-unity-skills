using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Qin.UnityEffects.Fluids
{
    internal sealed class FluidMixSimulation : IDisposable
    {
        internal const int ParticleCount = 384;
        private const int StepsPerSecond = 90;
        // Matches the parcel lifetime and timestamp modulus in FluidMix.compute.
        private const int FeedLifeTicks = 162;
        private const int FeedClockTicks = FeedLifeTicks * 4096;
        internal const float ContinuousDrainSeconds = FeedLifeTicks / (float)StepsPerSecond + .24f;
        private const int ParticleGroups = ParticleCount / 64;
        private const int GridWords = 25 * 18 * (1 + ParticleCount / 32) + 3 * 96 * (ParticleCount / 32);
        private const int FilmWidth = 256;
        private const int FilmHeight = 176;
        private const int WaveWidth = 128;
        private const int WaveHeight = 64;

        private static readonly int ParticlesId = Shader.PropertyToID("_Particles");
        private static readonly int PredictionsId = Shader.PropertyToID("_Predictions");
        private static readonly int CorrectionsId = Shader.PropertyToID("_Corrections");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int CellsId = Shader.PropertyToID("_Cells");
        private static readonly int ImpulsesId = Shader.PropertyToID("_Impulses");
        private static readonly int FilmId = Shader.PropertyToID("_Film");
        private static readonly int SprayId = Shader.PropertyToID("_Spray");
        private static readonly int WaveReadId = Shader.PropertyToID("_WaveRead");
        private static readonly int WaveWriteId = Shader.PropertyToID("_WaveWrite");
        private static readonly int TimeId = Shader.PropertyToID("_Seconds");
        private static readonly int AdmissionId = Shader.PropertyToID("_AdmissionUntil");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int FeedId = Shader.PropertyToID("_Feed");
        private static readonly int DurationId = Shader.PropertyToID("_Duration");
        private static readonly int CurtainHoldId = Shader.PropertyToID("_CurtainHold");
        private static readonly int FeedDurationId = Shader.PropertyToID("_FeedDuration");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int GravityId = Shader.PropertyToID("_Gravity");
        private static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        private static readonly int OriginId = Shader.PropertyToID("_Origin");
        private static readonly int VelocityId = Shader.PropertyToID("_Velocity");
        private static readonly int ObstacleId = Shader.PropertyToID("_Obstacle");
        private static readonly int PressureId = Shader.PropertyToID("_PressureScale");
        private static readonly int FreeFlightId = Shader.PropertyToID("_FreeFlight");
        private static readonly int BuildSupportMasksId = Shader.PropertyToID("_BuildSupportMasks");
        private static readonly int ContinuousFeedId = Shader.PropertyToID("_ContinuousFeed");
        private static readonly int FeedOpenId = Shader.PropertyToID("_FeedOpen");
        private static readonly int FlowTimesId = Shader.PropertyToID("_FlowTimes");
        private static readonly int FeedTickId = Shader.PropertyToID("_FeedTick");
        private static readonly int WavesEnabledId = Shader.PropertyToID("_WavesEnabled");
        private static readonly int SprayEnabledId = Shader.PropertyToID("_SprayEnabled");

        private readonly ComputeShader _program;
        private readonly CommandBuffer _commands;
        private readonly GraphicsBuffer _positions;
        private readonly GraphicsBuffer _corrected;
        private readonly GraphicsBuffer _density;
        private readonly GraphicsBuffer _cells;
        private readonly GraphicsBuffer _impulses;
        private readonly RenderTexture[] _waves = new RenderTexture[2];
        private readonly int _resetLiquid;
        private readonly int _resetWave;
        private readonly int _predict;
        private readonly int _clearCells;
        private readonly int _index;
        private readonly int _measure;
        private readonly int _project;
        private readonly int _velocity;
        private readonly int _stepWave;
        private readonly int _reconstruct;

        private int _waveIndex;
        private int _lastStep;
        private int _ballisticThroughStep;
        private bool _continuousFeed;
        private bool _hasLiquid;
        private bool _hasWaves;
        private bool _hasObstacle;
        private bool _denseVolume;
        private bool _disposed;

        internal RenderTexture Film { get; }
        internal RenderTexture Waves => _waves[_waveIndex];
        internal RenderTexture Spray { get; }
        internal GraphicsBuffer Particles { get; }
        internal long Step { get; private set; }

        internal FluidMixSimulation(ComputeShader program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));

            try
            {
                _program = UnityEngine.Object.Instantiate(program);
                _resetLiquid = _program.FindKernel("ResetLiquid");
                _resetWave = _program.FindKernel("ResetWave");
                _predict = _program.FindKernel("PredictPositions");
                _clearCells = _program.FindKernel("ClearCells");
                _index = _program.FindKernel("IndexParticles");
                _measure = _program.FindKernel("MeasureDensity");
                _project = _program.FindKernel("ProjectPressure");
                _velocity = _program.FindKernel("UpdateVelocities");
                _stepWave = _program.FindKernel("StepWave");
                _reconstruct = _program.FindKernel("ReconstructFilm");
                _commands = new CommandBuffer { name = "Fluid mixer fixed steps" };
                Particles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 32);
                _positions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 16);
                _corrected = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 16);
                _density = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 8);
                _cells = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GridWords, 4);
                _impulses = new GraphicsBuffer(GraphicsBuffer.Target.Structured, WaveWidth * WaveHeight, 4);
                Film = CreateTexture("Fluid thickness and front depth", FilmWidth, FilmHeight,
                    GraphicsFormat.R16G16B16A16_SFloat, FilterMode.Bilinear);
                Spray = CreateTexture("Fluid spray state", ParticleCount, 2,
                    GraphicsFormat.R32G32B32A32_SFloat, FilterMode.Point);
                for (int i = 0; i < _waves.Length; i++)
                    _waves[i] = CreateTexture("Fluid wave state", WaveWidth, WaveHeight,
                        GraphicsFormat.R32G32_SFloat, FilterMode.Bilinear);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal static bool Supported()
        {
            return SystemInfo.supportsComputeShaders
                && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.LoadStore)
                && SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, GraphicsFormatUsage.LoadStore)
                && SystemInfo.IsFormatSupported(GraphicsFormat.R32G32_SFloat, GraphicsFormatUsage.LoadStore);
        }

        internal void Reset(SOFluidMixRecipe recipe, uint seed)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            Step = 0;
            _waveIndex = 0;
            _lastStep = Mathf.CeilToInt(recipe.Duration * StepsPerSecond);
            _continuousFeed = recipe.ContinuousFeed;
            _hasLiquid = (int)recipe.Feed != 0;
            _hasWaves = recipe.SurfaceWaves;
            _hasObstacle = recipe.SolidObstacle;
            _denseVolume = recipe.Feed == SOFluidMixRecipe.Inlet.FallingMass;
            _ballisticThroughStep = BallisticThroughStep(recipe);
            _commands.Clear();
            _commands.SetComputeIntParam(_program, SeedId, unchecked((int)seed));
            _commands.SetComputeIntParam(_program, FeedId, (int)recipe.Feed);
            _commands.SetComputeFloatParam(_program, DurationId, recipe.Duration);
            _commands.SetComputeFloatParam(_program, CurtainHoldId, recipe.CurtainHold);
            _commands.SetComputeFloatParam(_program, FeedDurationId, recipe.FeedDuration);
            _commands.SetComputeFloatParam(_program, StrengthId, Mathf.Max(0f, recipe.Strength));
            _commands.SetComputeFloatParam(_program, GravityId, Mathf.Abs(recipe.Gravity));
            _commands.SetComputeFloatParam(_program, SwirlId, Mathf.Clamp(recipe.Swirl, 0f, 2f));
            _commands.SetComputeVectorParam(_program, OriginId, recipe.Origin);
            _commands.SetComputeVectorParam(_program, VelocityId, recipe.Velocity);
            _commands.SetComputeIntParam(_program, WavesEnabledId, _hasWaves ? 1 : 0);
            _commands.SetComputeIntParam(_program, SprayEnabledId, recipe.Spray ? 1 : 0);
            _commands.SetComputeFloatParam(_program, TimeId, 0f);
            _commands.SetComputeIntParam(_program, ContinuousFeedId, _continuousFeed ? 1 : 0);
            _commands.SetComputeIntParam(_program, FeedTickId, 0);
            _commands.SetComputeVectorParam(_program, FlowTimesId, Vector4.zero);
            Bind(_resetLiquid, ParticlesId, Particles);
            Bind(_resetLiquid, DensityId, _density);
            _commands.SetComputeTextureParam(_program, _resetLiquid, SprayId, Spray);
            Dispatch(_resetLiquid, ParticleGroups);
            Bind(_resetWave, ImpulsesId, _impulses);
            for (int i = 0; i < _waves.Length; i++)
            {
                _commands.SetComputeTextureParam(_program, _resetWave, WaveWriteId, _waves[i]);
                _commands.DispatchCompute(_program, _resetWave, WaveWidth / 8, WaveHeight / 8, 1);
            }
            ClearCells();
            ReconstructFilm();
            Graphics.ExecuteCommandBuffer(_commands);
        }

        internal bool Advance(double elapsed, double admissionUntil, Vector4 obstacle, float pressureScale = 1f)
        {
            long target = _continuousFeed ? (long)Math.Floor(elapsed * StepsPerSecond + .001)
                : Mathf.Min(_lastStep, Mathf.FloorToInt((float)elapsed * StepsPerSecond + .001f));
            if (target <= Step)
                return false;

            _commands.Clear();
            _commands.SetComputeFloatParam(_program, AdmissionId, (float)admissionUntil);
            _commands.SetComputeFloatParam(_program, PressureId, Mathf.Clamp(pressureScale, 0f, 2f));
            _commands.SetComputeVectorParam(_program, ObstacleId, _hasObstacle ? obstacle : Vector4.zero);
            while (Step < target)
            {
                Step++;
                if (_continuousFeed)
                {
                    int clock = (int)((Step - 1) % FeedClockTicks);
                    _commands.SetComputeIntParam(_program, FeedTickId, clock);
                    _commands.SetComputeIntParam(_program, FeedOpenId, Step / (double)StepsPerSecond <= admissionUntil ? 1 : 0);
                    _commands.SetComputeFloatParam(_program, TimeId, clock / (float)StepsPerSecond);
                }
                else
                {
                    _commands.SetComputeFloatParam(_program, TimeId, Step / (float)StepsPerSecond);
                }
                if (_hasLiquid)
                    IntegrateLiquid();
                if (_hasWaves)
                    IntegrateWave();
            }
            if (_hasLiquid)
            {
                if (_continuousFeed)
                {
                    float sinceOpening = (float)Math.Min(2.0, elapsed);
                    float sinceClosing = (float)Math.Clamp(elapsed - admissionUntil, 0.0, 2.0);
                    _commands.SetComputeVectorParam(_program, FlowTimesId, new Vector4(sinceOpening, sinceClosing, 0f, 0f));
                }
                Index(_positions);
                ReconstructFilm();
            }
            Graphics.ExecuteCommandBuffer(_commands);
            return true;
        }

        private static RenderTexture CreateTexture(string label, int width, int height, GraphicsFormat format,
            FilterMode filter)
        {
            var descriptor = new RenderTextureDescriptor(width, height, format, 0)
            {
                enableRandomWrite = true,
                useMipMap = false
            };
            var texture = new RenderTexture(descriptor) { name = label, filterMode = filter, wrapMode = TextureWrapMode.Clamp };
            if (texture.Create())
                return texture;
            ReleaseTexture(texture);
            throw new InvalidOperationException("Unable to allocate " + label);
        }

        private static int BallisticThroughStep(SOFluidMixRecipe recipe)
        {
            if (recipe.Feed == SOFluidMixRecipe.Inlet.Needle)
                return Mathf.CeilToInt(recipe.Duration * StepsPerSecond);
            if (recipe.Feed != SOFluidMixRecipe.Inlet.FallingMass || recipe.SolidObstacle)
                return 0;
            // .35 is the release semi-height; .04 is the floor and .14 covers the maximum wave lift and a margin.
            float clearance = recipe.Origin.y - .35f - .04f - .14f;
            if (clearance <= 0f)
                return 0;
            float downward = Mathf.Max(0f, -recipe.Velocity.y * Mathf.Sqrt(recipe.Strength));
            float closingSpeed = downward + Mathf.Sqrt(downward * downward + 2f * recipe.Gravity * clearance);
            float flight = closingSpeed > .0001f ? 2f * clearance / closingSpeed : recipe.Duration;
            float opening = Mathf.Min(.10f, .47f * recipe.FeedDuration);
            return Mathf.FloorToInt(Mathf.Min(recipe.Duration, opening + flight) * StepsPerSecond);
        }

        private void Bind(int kernel, int property, GraphicsBuffer buffer)
        {
            _commands.SetComputeBufferParam(_program, kernel, property, buffer);
        }

        private void Dispatch(int kernel, int groups)
        {
            _commands.DispatchCompute(_program, kernel, groups, 1, 1);
        }

        private void ClearCells()
        {
            Bind(_clearCells, CellsId, _cells);
            Dispatch(_clearCells, (GridWords + 63) / 64);
        }

        private void Index(GraphicsBuffer positions, bool forPressure = false)
        {
            ClearCells();
            _commands.SetComputeIntParam(_program, BuildSupportMasksId, forPressure && _denseVolume ? 1 : 0);
            Bind(_index, CellsId, _cells);
            Bind(_index, PredictionsId, positions);
            Dispatch(_index, ParticleGroups);
        }

        private void Correct(GraphicsBuffer positions, GraphicsBuffer corrected)
        {
            Index(positions, true);
            Bind(_measure, PredictionsId, positions);
            Bind(_measure, DensityId, _density);
            Bind(_measure, CellsId, _cells);
            Dispatch(_measure, ParticleGroups);
            Bind(_project, PredictionsId, positions);
            Bind(_project, CorrectionsId, corrected);
            Bind(_project, DensityId, _density);
            Bind(_project, CellsId, _cells);
            Dispatch(_project, ParticleGroups);
        }

        private void IntegrateLiquid()
        {
            bool freeFlight = Step <= _ballisticThroughStep;
            _commands.SetComputeIntParam(_program, FreeFlightId, freeFlight ? 1 : 0);
            Bind(_predict, ParticlesId, Particles);
            Bind(_predict, PredictionsId, _positions);
            Bind(_predict, ImpulsesId, _impulses);
            _commands.SetComputeTextureParam(_program, _predict, WaveReadId, Waves);
            Dispatch(_predict, ParticleGroups);
            if (!freeFlight)
            {
                Correct(_positions, _corrected);
                Correct(_corrected, _positions);
            }
            Bind(_velocity, PredictionsId, _positions);
            Bind(_velocity, ParticlesId, Particles);
            Bind(_velocity, DensityId, _density);
            _commands.SetComputeTextureParam(_program, _velocity, SprayId, Spray);
            Dispatch(_velocity, ParticleGroups);
        }

        private void IntegrateWave()
        {
            int next = 1 - _waveIndex;
            Bind(_stepWave, ImpulsesId, _impulses);
            _commands.SetComputeTextureParam(_program, _stepWave, WaveReadId, Waves);
            _commands.SetComputeTextureParam(_program, _stepWave, WaveWriteId, _waves[next]);
            _commands.DispatchCompute(_program, _stepWave, WaveWidth / 8, WaveHeight / 8, 1);
            _waveIndex = next;
        }

        private void ReconstructFilm()
        {
            Bind(_reconstruct, ParticlesId, Particles);
            Bind(_reconstruct, CellsId, _cells);
            _commands.SetComputeTextureParam(_program, _reconstruct, FilmId, Film);
            _commands.DispatchCompute(_program, _reconstruct, FilmWidth / 8, FilmHeight / 8, 1);
        }

        private static void DestroyResource(UnityEngine.Object resource)
        {
            if (resource == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(resource);
            else
                UnityEngine.Object.DestroyImmediate(resource);
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            DestroyResource(texture);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _commands?.Release();
            Particles?.Dispose();
            _positions?.Dispose();
            _corrected?.Dispose();
            _density?.Dispose();
            _cells?.Dispose();
            _impulses?.Dispose();
            ReleaseTexture(Film);
            ReleaseTexture(Spray);
            foreach (RenderTexture wave in _waves)
                ReleaseTexture(wave);
            DestroyResource(_program);
        }
    }
}
