using Unity.Collections;
using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class EnemyChaseController : MonoBehaviour
    {
        private static readonly int Parameter = Animator.StringToHash("Chasing");

        [SerializeField] private Animator _animator;

        private EnemyChaseManager _owner;
        private int _slot;

        private void OnEnable()
        {
            _owner.StateCompleted += ApplyState;
        }

        private void OnDisable()
        {
            _owner.StateCompleted -= ApplyState;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _owner.HitPlayer(_slot, other);
        }
        private void ApplyState(NativeArray<EnemyChaseState>.ReadOnly states)
        {
            EnemyChaseState state = states[_slot];
            bool active = state.IsChasing;
            if (_animator.GetBool(Parameter) != active) _animator.SetBool(Parameter, active);
        }

        public void Bind(EnemyChaseManager owner, int slot)
        {
            _owner = owner;
            _slot = slot;
        }
    }
}
