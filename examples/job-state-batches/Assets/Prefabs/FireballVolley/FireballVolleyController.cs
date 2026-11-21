using Unity.Collections;
using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class FireballVolleyController : MonoBehaviour
    {
        [SerializeField] private GameObject _visual;

        private FireballVolleyManager _owner;
        private int _slot;

        private void OnEnable()
        {
            _owner.StateCompleted += ApplyState;
            SetVisual(_owner.ReadState(_slot));
        }

        private void OnDisable()
        {
            _owner.StateCompleted -= ApplyState;
        }

        private void ApplyState(NativeArray<FireballVolleyState>.ReadOnly states)
        {
            SetVisual(states[_slot]);
        }

        private void SetVisual(FireballVolleyState state)
        {
            bool visible = state.RemainingDistance > 0f;
            if (_visual.activeSelf != visible) _visual.SetActive(visible);
        }

        public void Bind(FireballVolleyManager owner, int slot)
        {
            _owner = owner;
            _slot = slot;
        }
    }
}
