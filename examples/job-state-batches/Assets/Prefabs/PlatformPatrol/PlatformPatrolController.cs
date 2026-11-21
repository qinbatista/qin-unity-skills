using Unity.Collections;
using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class PlatformPatrolController : MonoBehaviour
    {
        private static readonly int Parameter = Animator.StringToHash("Holding");

        [SerializeField] private Animator _animator;

        private PlatformPatrolManager _owner;
        private int _slot;

        private void OnEnable()
        {
            _owner.StateCompleted += ApplyState;
        }

        private void OnDisable()
        {
            _owner.StateCompleted -= ApplyState;
        }

        private void ApplyState(NativeArray<PlatformPatrolState>.ReadOnly states)
        {
            PlatformPatrolState state = states[_slot];
            bool active = state.Phase == PatrolPhase.HoldAtEnd || state.Phase == PatrolPhase.HoldAtStart;
            if (_animator.GetBool(Parameter) != active) _animator.SetBool(Parameter, active);
        }

        public void Bind(PlatformPatrolManager owner, int slot)
        {
            _owner = owner;
            _slot = slot;
        }
    }
}
