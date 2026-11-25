using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    static class LayoutFeedback
    {
        public static void Enter(VisualElement screen, bool reduceMotion)
        {
            screen.EnableInClassList("reduced-motion", reduceMotion);
            if (reduceMotion) return;

            screen.AddToClassList("view-entering");
            screen.RegisterCallback<GeometryChangedEvent>(OnFirstLayout);
        }

        static void OnFirstLayout(GeometryChangedEvent change)
        {
            VisualElement screen = (VisualElement)change.currentTarget;
            screen.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);
            screen.RemoveFromClassList("view-entering");
        }
    }
}
