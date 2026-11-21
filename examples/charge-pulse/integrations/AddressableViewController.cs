using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Qin.UnitySamples
{
    public sealed class AddressableViewController : MonoBehaviour
    {
        // The registered prefab is authored inactive so a superseded load cannot appear.
        [SerializeField] private string _address;
        private AsyncOperationHandle<GameObject>? _lease;

        private void OnEnable()
        {
            AsyncOperationHandle<GameObject> lease = Addressables.InstantiateAsync(_address, transform);
            _lease = lease;
            lease.Completed += OnLoaded;
        }

        private void OnDisable()
        {
            AsyncOperationHandle<GameObject>? previous = _lease;
            _lease = null;
            if (!previous.HasValue || !previous.Value.IsDone) return;
            AsyncOperationHandle<GameObject> lease = previous.Value;
            lease.Completed -= OnLoaded;
            Retire(lease);
        }

        private void OnLoaded(AsyncOperationHandle<GameObject> lease)
        {
            lease.Completed -= OnLoaded;
            if (_lease.HasValue && _lease.Value.Equals(lease))
            {
                if (lease.Status == AsyncOperationStatus.Succeeded)
                {
                    lease.Result.SetActive(true);
                    return;
                }
                _lease = null;
                Debug.LogException(lease.OperationException, this);
            }
            Retire(lease);
        }

        private static void Retire(AsyncOperationHandle<GameObject> lease)
        {
            if (lease.Status == AsyncOperationStatus.Succeeded)
            {
                lease.Result.SetActive(false);
                Addressables.ReleaseInstance(lease);
                return;
            }
            Addressables.Release(lease);
        }
    }
}
