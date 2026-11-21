using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples
{
    public enum ReviewLanguage { English, German }

    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class ReviewScreenController : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _viewport;
        private VisualElement _home;
        private VisualElement _details;
        private VisualElement _returnFocus;
        private Button _open;
        private Button _back;
        private Label _heading;
        private Label _detailHeading;
        private Label _summary;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            _viewport = _document.rootVisualElement.Q("review-viewport");
            _home = _viewport.Q("home");
            _details = _viewport.Q("details");
            _heading = _home.Q<Label>("heading");
            _detailHeading = _details.Q<Label>("detail-heading");
            _summary = _details.Q<Label>("summary");
            _open = _home.Q<Button>("open");
            _back = _details.Q<Button>("back");
            _open.clicked += OpenDetails;
            _back.clicked += CloseDetails;
        }

        private void OnDisable()
        {
            _open.clicked -= OpenDetails;
            _back.clicked -= CloseDetails;
            _details.style.display = DisplayStyle.None;
            _home.SetEnabled(true);
            _returnFocus = null;
        }

        private void OpenDetails()
        {
            _returnFocus = _open.focusController.focusedElement as VisualElement;
            _home.SetEnabled(false);
            _details.style.display = DisplayStyle.Flex;
            _back.Focus();
        }

        private void CloseDetails()
        {
            _details.style.display = DisplayStyle.None;
            _home.SetEnabled(true);
            (_returnFocus ?? _open).Focus();
            _returnFocus = null;
        }

        public void SetLanguage(ReviewLanguage language)
        {
            switch (language)
            {
                case ReviewLanguage.English:
                    _heading.text = "Charge review";
                    _detailHeading.text = "Release timing";
                    _summary.text = "The ring reaches its maximum after the spark burst.";
                    _open.text = "Details";
                    _back.text = "Back";
                    break;
                case ReviewLanguage.German:
                    _heading.text = "Überprüfung der Aufladeanimation";
                    _detailHeading.text = "Zeitpunkt der Freisetzung";
                    _summary.text = "Der Ring erreicht seine maximale Größe nach dem Funkenstoß.";
                    _open.text = "Details";
                    _back.text = "Zurück";
                    break;
            }
        }

        public void ApplyInsets(Rect displayPixels, Rect safePixels)
        {
            float unitsPerPixel = _viewport.worldBound.width / displayPixels.width;
            _viewport.style.paddingLeft = (safePixels.xMin - displayPixels.xMin) * unitsPerPixel;
            _viewport.style.paddingRight = (displayPixels.xMax - safePixels.xMax) * unitsPerPixel;
            _viewport.style.paddingTop = (displayPixels.yMax - safePixels.yMax) * unitsPerPixel;
            _viewport.style.paddingBottom = (safePixels.yMin - displayPixels.yMin) * unitsPerPixel;
        }
    }
}
