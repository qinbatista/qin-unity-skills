using UnityEngine;
using UnityEngine.VFX;

namespace Qin.UnitySamples
{
    [RequireComponent(typeof(VisualEffect))]
    public sealed class GraphEffectController : MonoBehaviour
    {
        private static readonly int _burst = Shader.PropertyToID("Burst");
        private static readonly int _strength = Shader.PropertyToID("BurstStrength");
        private VisualEffect _effect;

        private void Awake()
        {
            _effect = GetComponent<VisualEffect>();
        }

        private void OnDisable()
        {
            _effect.Stop();
            _effect.enabled = false;
        }

        public void PlayBurst(float strength)
        {
            if (!isActiveAndEnabled) return;
            _effect.enabled = true;
            _effect.Reinit();
            _effect.SetFloat(_strength, Mathf.Clamp01(strength));
            _effect.SendEvent(_burst);
        }
    }
}
