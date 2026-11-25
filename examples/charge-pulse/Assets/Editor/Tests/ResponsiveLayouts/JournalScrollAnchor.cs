using System;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    // Follow the final entry across a resize only when the reader was already at the end.
    // For readers in the middle, ScrollView retains its normal content offset.
    sealed class JournalScrollAnchor : IDisposable
    {
        readonly ScrollView list;
        VisualElement end;

        public JournalScrollAnchor(ScrollView list)
        {
            this.list = list;
            list.contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportChanged);
        }

        public void BeforeViewportChange()
        {
            end = null;
            if (list.contentViewport.contentRect.height <= 0f || list.contentContainer.childCount == 0)
                return;

            VisualElement last = list.contentContainer.ElementAt(list.contentContainer.childCount - 1);
            if (last.worldBound.yMax <= list.contentViewport.worldBound.yMax + 1f)
                end = last;
        }

        void OnViewportChanged(GeometryChangedEvent change)
        {
            if (end != null && change.oldRect.size != change.newRect.size)
                list.ScrollTo(end);
        }

        public void Dispose()
        {
            list.contentViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportChanged);
        }
    }
}
