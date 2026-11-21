using UnityEngine;

namespace Qin.UnitySamples
{
    public sealed class TimedRevealController : MonoBehaviour
    {
        private Transform _visual;
        private float _duration;
        private float _startedAt;
        private float _progress;
        private float _from;
        private float _target;

        private void Awake()
        {
            _visual = transform;
            enabled = false;
        }

        private void Update()
        {
            _progress = Mathf.Clamp01((Time.unscaledTime - _startedAt) / _duration);
            _visual.localScale = Vector3.one * Mathf.SmoothStep(_from, _target, _progress);
            if (_progress >= 1f) enabled = false;
        }

        public void ScaleTo(float target, float duration)
        {
            Vector3 current = _visual.localScale;
            if (duration <= 0f || Mathf.Approximately(current.x, target))
            {
                Vector3 endpoint = Vector3.one * target;
                if (!current.Equals(endpoint)) _visual.localScale = endpoint;
                enabled = false;
                return;
            }

            _from = current.x;
            _target = target;
            _duration = duration;
            _startedAt = Time.unscaledTime;
            enabled = true;
        }
    }
}
