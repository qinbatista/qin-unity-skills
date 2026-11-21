using Unity.Collections;
using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class CoinMagnetController : MonoBehaviour
    {
        [SerializeField] private GameObject _visual;

        private CoinMagnetManager _owner;
        private int _slot;

        private void OnEnable()
        {
            _owner.StateCompleted += ApplyState;
        }

        private void OnDisable()
        {
            _owner.StateCompleted -= ApplyState;
        }

        private void ApplyState(NativeArray<CoinMagnetState>.ReadOnly states)
        {
            SetVisual(states[_slot]);
        }

        private void SetVisual(CoinMagnetState state)
        {
            bool visible = state.Phase != CoinPhase.Collected;
            if (_visual.activeSelf != visible) _visual.SetActive(visible);
        }

        public void Bind(CoinMagnetManager owner, int slot)
        {
            _owner = owner;
            _slot = slot;
        }
    }
}
