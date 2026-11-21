using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    [RequireComponent(typeof(CompanionFormationManager))]
    public sealed class CompanionFormationSceneController : MonoBehaviour
    {
        [SerializeField] private CompanionFormationController _prefab;
        [SerializeField] private Transform _leader;
        [SerializeField] private Transform[] _starts;
        [SerializeField] private Vector3[] _offsets;
        [SerializeField, Min(0.01f)] private float _speed = 4f;

        private void Start()
        {
            GetComponent<CompanionFormationManager>().Spawn(_prefab, _leader, _starts, _offsets, _speed);
        }
    }
}
