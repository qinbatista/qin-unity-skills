using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    [RequireComponent(typeof(CoinMagnetManager))]
    public sealed class CoinMagnetSceneController : MonoBehaviour
    {
        [SerializeField] private CoinMagnetController _prefab;
        [SerializeField] private Transform _collector;
        [SerializeField] private Transform[] _points;
        [SerializeField, Min(0.01f)] private float _radius = 2.5f;
        [SerializeField, Min(0.01f)] private float _speed = 3f;

        private void Start()
        {
            GetComponent<CoinMagnetManager>().Spawn(_prefab, _collector, _points, _radius, _speed);
        }
    }
}
