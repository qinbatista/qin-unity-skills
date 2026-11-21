using UnityEngine;

namespace Qin.UnitySamples.JobStateBatches
{
    [RequireComponent(typeof(FireballVolleyManager))]
    public sealed class FireballVolleySceneController : MonoBehaviour
    {
        [SerializeField] private FireballVolleyController _prefab;
        [SerializeField] private Transform _muzzle;
        [SerializeField, Min(0.01f)] private float _speed = 4f;
        [SerializeField, Min(0.01f)] private float _range = 4.8f;
        [SerializeField, Range(1f, 30f)] private float _angleStep = 12f;

        private void Start()
        {
            GetComponent<FireballVolleyManager>().Fire(_prefab, _muzzle, _speed, _range, _angleStep);
        }
    }
}
