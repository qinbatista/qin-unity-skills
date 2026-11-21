using UnityEngine;
using UnityEngine.Serialization;

namespace Qin.UnitySamples
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class StepMotionController : MonoBehaviour
    {
        [FormerlySerializedAs("_indicator")]
        [SerializeField] private Transform _marker;
        private Rigidbody _body;
        private Transform _pose;
        private Vector3 _velocity;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _pose = transform;
            enabled = false;
        }

        private void FixedUpdate()
        {
            _body.MovePosition(_body.position + _velocity * Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (_marker.position != _pose.position || _marker.rotation != _pose.rotation)
                _marker.SetPositionAndRotation(_pose.position, _pose.rotation);
        }

        public void SetVelocity(Vector3 velocity)
        {
            _velocity = velocity;
            enabled = velocity != Vector3.zero;
        }
    }
}
