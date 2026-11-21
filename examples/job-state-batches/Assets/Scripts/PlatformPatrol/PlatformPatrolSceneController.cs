using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    [RequireComponent(typeof(PlatformPatrolManager))]
    public sealed class PlatformPatrolSceneController : MonoBehaviour
    {
        [SerializeField] private PlatformPatrolController _prefab;
        [SerializeField] private Transform[] _starts;
        [SerializeField] private Transform[] _ends;
        [SerializeField, Min(0.01f)] private float _speed = 2f;
        [SerializeField, Min(0.01f)] private float _hold = 0.45f;

        private void Start()
        {
            GetComponent<PlatformPatrolManager>().Spawn(_prefab, _starts, _ends, _speed, _hold);
        }
    }
}
