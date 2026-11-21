using Unity.Collections;
using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class CompanionFormationController : MonoBehaviour
    {
        private static readonly int Parameter = Animator.StringToHash("Following");

        [SerializeField] private Animator _animator;

        private CompanionFormationManager _owner;
        private int _slot;

        private void OnEnable()
        {
            _owner.StateCompleted += ApplyState;
        }

        private void OnDisable()
        {
            _owner.StateCompleted -= ApplyState;
        }

        private void ApplyState(NativeArray<CompanionFormationState>.ReadOnly states)
        {
            CompanionFormationState state = states[_slot];
            bool active = state.IsFollowing;
            if (_animator.GetBool(Parameter) != active) _animator.SetBool(Parameter, active);
        }

        public void Bind(CompanionFormationManager owner, int slot)
        {
            _owner = owner;
            _slot = slot;
        }
    }
}
