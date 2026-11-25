using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    // One grid owns pointer capture and action routing for every existing item instance.
    sealed class InventoryReorder
    {
        readonly VisualElement screen, grid;
        VisualElement pressed, selected, target;
        Vector2 origin;
        int pointer = -1;
        bool dragging;

        public int Moves { get; private set; }

        public InventoryReorder(VisualElement screen)
        {
            this.screen = screen;
            grid = screen.Q("tiles");
            grid.RegisterCallback<PointerDownEvent>(Down);
            grid.RegisterCallback<PointerMoveEvent>(Move);
            grid.RegisterCallback<PointerUpEvent>(Up);
            grid.RegisterCallback<PointerCancelEvent>(Cancel);
            grid.RegisterCallback<PointerCaptureOutEvent>(LostCapture);
            grid.RegisterCallback<NavigationSubmitEvent>(Submit);
            grid.RegisterCallback<NavigationCancelEvent>(CancelSelection);
            grid.RegisterCallback<FocusInEvent>(FocusDestination);
            grid.RegisterCallback<GeometryChangedEvent>(Resized);
            grid.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        VisualElement Tile(VisualElement element)
        {
            while (element != null && element.parent != grid) element = element.parent;
            return element != null && element.ClassListContains("tile") ? element : null;
        }

        void Down(PointerDownEvent evt)
        {
            VisualElement tile = Tile(evt.target as VisualElement);
            if (pointer >= 0 || evt.button != 0 || tile == null) return;
            pressed = tile;
            pressed.Focus();
            pointer = evt.pointerId;
            origin = evt.position;
            grid.CapturePointer(pointer);
            evt.StopPropagation();
        }

        void Move(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer) return;
            Vector2 position = evt.position;
            if (!dragging && (position - origin).sqrMagnitude >= 36f)
            {
                dragging = true;
                Select(pressed);
            }
            if (dragging) Target(Tile(grid.panel.Pick(position)));
        }

        void Up(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer) return;
            VisualElement destination = Tile(grid.panel.Pick(evt.position));
            VisualElement source = pressed;
            bool drop = dragging;
            EndContact();
            if (drop) Finish(destination);
            else if (destination == source) Activate(source);
            else Select(null);
            evt.StopPropagation();
        }

        void EndContact()
        {
            int prior = pointer;
            pointer = -1;
            pressed = null;
            dragging = false;
            if (prior >= 0 && grid.HasPointerCapture(prior)) grid.ReleasePointer(prior);
        }

        void Cancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == pointer) Reset();
        }

        void LostCapture(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == pointer) Reset();
        }

        void Resized(GeometryChangedEvent evt)
        {
            if (evt.oldRect.size != evt.newRect.size) Reset();
        }

        void Detached(DetachFromPanelEvent _) => Reset();

        void Reset()
        {
            EndContact();
            Select(null);
        }

        void Submit(NavigationSubmitEvent evt)
        {
            VisualElement item = Tile(evt.target as VisualElement);
            if (item == null || pointer >= 0) return;
            Activate(item);
            evt.StopPropagation();
        }

        void Activate(VisualElement item)
        {
            if (selected == null) Select(item);
            else Finish(item);
        }

        void CancelSelection(NavigationCancelEvent evt)
        {
            if (selected == null) return;
            VisualElement source = selected;
            Reset();
            source.Focus();
            evt.StopPropagation();
        }

        void FocusDestination(FocusInEvent evt)
        {
            if (selected != null && !dragging) Target(Tile(evt.target as VisualElement));
        }

        void Select(VisualElement item)
        {
            selected?.RemoveFromClassList("move-source");
            Target(null);
            selected = item;
            selected?.AddToClassList("move-source");
            screen.EnableInClassList("inventory-moving", selected != null);
        }

        void Target(VisualElement item)
        {
            if (item == selected) item = null;
            if (item == target) return;
            target?.RemoveFromClassList("drop-target");
            target = item;
            target?.AddToClassList("drop-target");
        }

        void Finish(VisualElement destination)
        {
            VisualElement source = selected;
            Select(null);
            if (source == null || destination == null || source == destination) return;
            if (grid.IndexOf(source) < grid.IndexOf(destination)) source.PlaceInFront(destination);
            else source.PlaceBehind(destination);
            Moves++;
            source.Focus();
        }
    }
}
