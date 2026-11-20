using System;
using UnityEngine;

namespace Qin.UnitySamples
{
    public enum PulseStage { Recycled, Prepare, Preparing, Prepared, Releasing, Released, Retracting }

    public sealed class ChargePulseController : MonoBehaviour
    {
        public const string IdleState = "Idle";
        public const string PulseState = "Pulse";

        private static readonly int _idleStateHash = Animator.StringToHash("Base Layer." + IdleState);
        private static readonly int _pulseStateHash = Animator.StringToHash("Base Layer." + PulseState);

        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _visuals;
        [SerializeField] private ParticleSystem _sparks;

        public PulseStage Stage { get; private set; }
        public event Action<PulseStage> StageChanged;

        private void OnEnable()
        {
            _visuals.SetActive(true);
            _animator.Play(_idleStateHash, 0, 0f);
        }

        private void OnDisable()
        {
            _sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _visuals.SetActive(false);
            SetStage(PulseStage.Recycled);
        }

        public void StartPulse()
        {
            if (!isActiveAndEnabled || Stage != PulseStage.Recycled) return;
            _animator.Play(_pulseStateHash, 0, 0f);
            SetStage(PulseStage.Prepare);
        }

        public void ApplyAnimationStage(int stageIndex)
        {
            if (!isActiveAndEnabled || Stage == PulseStage.Recycled) return;

            PulseStage nextStage = Stage == PulseStage.Retracting ? PulseStage.Recycled : Stage + 1;
            if (stageIndex != (int)nextStage) return;

            switch (nextStage)
            {
                case PulseStage.Releasing:
                    _sparks.Play(false);
                    break;
                case PulseStage.Recycled:
                    _sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _animator.Play(_idleStateHash, 0, 0f);
                    break;
            }

            SetStage(nextStage);
        }

        private void SetStage(PulseStage stage)
        {
            if (Stage == stage) return;
            Stage = stage;
            StageChanged?.Invoke(stage);
        }
    }
}
