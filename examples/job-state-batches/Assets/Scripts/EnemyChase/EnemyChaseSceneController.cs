using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    [RequireComponent(typeof(EnemyChaseManager))]
    public sealed class EnemyChaseSceneController : MonoBehaviour
    {
        [SerializeField] private EnemyChaseController _prefab;
        [SerializeField] private Collider2D _target;
        [SerializeField] private Transform[] _points;
        [SerializeField, Min(0.01f)] private float _speed = 3f;

        private void Start()
        {
            GetComponent<EnemyChaseManager>().Spawn(_prefab, _target, _points, _speed);
        }
    }
}
