using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples
{
    [DefaultExecutionOrder(100)]
    public sealed class ChargePulseHudController : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private ChargePulseController _pulse;

        private Button _chargeButton;
        private Label _status;

        private void OnEnable()
        {
            VisualElement root = _document.rootVisualElement;
            _chargeButton = root.Q<Button>("charge");
            _status = root.Q<Label>("status");
            _chargeButton.clicked += _pulse.StartPulse;
            _pulse.StageChanged += ShowStage;
            ShowStage(_pulse.Stage);
        }

        private void OnDisable()
        {
            _chargeButton.clicked -= _pulse.StartPulse;
            _pulse.StageChanged -= ShowStage;
        }

        private void ShowStage(PulseStage stage)
        {
            switch (stage)
            {
                case PulseStage.Recycled:
                    _status.text = "Ready";
                    break;
                case PulseStage.Releasing:
                case PulseStage.Released:
                    _status.text = "Releasing";
                    break;
                case PulseStage.Retracting:
                    _status.text = "Recovering";
                    break;
                default:
                    _status.text = "Charging";
                    break;
            }

            _chargeButton.SetEnabled(stage == PulseStage.Recycled);
        }
    }
}
