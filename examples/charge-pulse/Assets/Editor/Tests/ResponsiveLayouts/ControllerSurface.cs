using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    sealed class ControllerSurface : IDisposable
    {
        readonly VisualElement screen, field, marker, rail, stickGroup, stick, thumb, actions;
        readonly Button pulse, pause, resume;
        readonly IVisualElementScheduledItem pulseEnd;
        int stickPointer = -1;
        Rect exclusion;
        Focusable returnFocus;

        public Vector2 Direction { get; private set; }
        public int Pulses { get; private set; }
        public bool Paused { get; private set; }

        public ControllerSurface(VisualElement screen)
        {
            this.screen = screen;
            field = screen.Q("field");
            marker = screen.Q("marker");
            rail = screen.Q("rail");
            stickGroup = screen.Q("stick-group");
            stick = screen.Q("stick");
            thumb = screen.Q("thumb");
            actions = screen.Q("actions");
            pulse = screen.Q<Button>("pulse");
            pause = screen.Q<Button>("pause");
            resume = screen.Q<Button>("resume");
            pulseEnd = field.schedule.Execute(() => field.RemoveFromClassList("pulsed"));
            pulseEnd.Pause();
            stick.RegisterCallback<PointerDownEvent>(StartDrag);
            stick.RegisterCallback<PointerMoveEvent>(Drag);
            stick.RegisterCallback<PointerUpEvent>(Release);
            stick.RegisterCallback<PointerCancelEvent>(Cancel);
            stick.RegisterCallback<PointerCaptureOutEvent>(LostCapture);
            rail.RegisterCallback<GeometryChangedEvent>(FitRail);
            screen.RegisterCallback<NavigationCancelEvent>(OnCancel);
            screen.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            pulse.clicked += OnPulse;
            pause.clicked += OpenPause;
            resume.clicked += ClosePause;
        }

        // Input is the local OS exclusion projected into the panel's coordinate system.
        public void SetExclusion(Rect panelExclusion)
        {
            ResetStick();
            exclusion = panelExclusion;
            FitGroups();
        }

        void FitRail(GeometryChangedEvent _) => FitGroups();

        void FitGroups()
        {
            FitGroup(stickGroup);
            FitGroup(actions);
        }

        void FitGroup(VisualElement group)
        {
            Rect bounds = group.worldBound;
            if (bounds.width <= 0f || float.IsNaN(bounds.height)) return;
            // Reconstruct its unshifted bottom anchor, including while the previous exclusion is active.
            bounds.y = rail.worldBound.yMax - bounds.height;
            float rise = exclusion.width > 0f && bounds.Overlaps(exclusion) ? bounds.yMax - exclusion.yMin + 8f : 0f;
            group.style.bottom = rise;
        }

        void StartDrag(PointerDownEvent evt)
        {
            if (Paused || stickPointer != -1 || evt.button != 0) return;
            stickPointer = evt.pointerId;
            stick.CapturePointer(stickPointer);
            stick.AddToClassList("held");
            SetDirection(evt.position);
            evt.StopPropagation();
        }

        void Drag(PointerMoveEvent evt)
        {
            if (evt.pointerId != stickPointer) return;
            SetDirection(evt.position);
            evt.StopPropagation();
        }

        void SetDirection(Vector2 panelPosition)
        {
            float travel = (stick.contentRect.width - thumb.layout.width) * 0.5f;
            if (travel <= 0f) return;
            Vector2 displacement = Vector2.ClampMagnitude(stick.WorldToLocal(panelPosition) - stick.contentRect.center, travel);
            Direction = new Vector2(displacement.x, -displacement.y) / travel;
            thumb.style.translate = new Translate(displacement.x, displacement.y);
            marker.style.translate = new Translate(Direction.x * 30f, -Direction.y * 30f);
        }

        void Release(PointerUpEvent evt)
        {
            if (evt.pointerId == stickPointer) ResetStick();
        }

        void Cancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == stickPointer) ResetStick();
        }

        void LostCapture(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == stickPointer) ResetStick();
        }

        void ResetStick()
        {
            int previous = stickPointer;
            stickPointer = -1;
            if (previous >= 0 && stick.HasPointerCapture(previous)) stick.ReleasePointer(previous);
            Direction = Vector2.zero;
            thumb.style.translate = new Translate(0f, 0f);
            marker.style.translate = new Translate(0f, 0f);
            stick.RemoveFromClassList("held");
        }

        void OnPulse()
        {
            if (Paused) return;
            Pulses++;
            field.AddToClassList("pulsed");
            pulseEnd.ExecuteLater(180);
        }

        void OpenPause()
        {
            if (Paused) return;
            Paused = true;
            returnFocus = screen.focusController.focusedElement ?? pause;
            ResetStick();
            rail.SetEnabled(false);
            screen.AddToClassList("paused");
            resume.Focus();
        }

        void ClosePause()
        {
            if (!Paused) return;
            Paused = false;
            screen.RemoveFromClassList("paused");
            rail.SetEnabled(true);
            returnFocus.Focus();
        }

        void OnCancel(NavigationCancelEvent evt)
        {
            if (Paused) ClosePause();
            else OpenPause();
            evt.StopPropagation();
        }

        void OnDetach(DetachFromPanelEvent _) => Dispose();

        public void Dispose()
        {
            ResetStick();
            pulseEnd.Pause();
            pulse.clicked -= OnPulse;
            pause.clicked -= OpenPause;
            resume.clicked -= ClosePause;
            stick.UnregisterCallback<PointerDownEvent>(StartDrag);
            stick.UnregisterCallback<PointerMoveEvent>(Drag);
            stick.UnregisterCallback<PointerUpEvent>(Release);
            stick.UnregisterCallback<PointerCancelEvent>(Cancel);
            stick.UnregisterCallback<PointerCaptureOutEvent>(LostCapture);
            rail.UnregisterCallback<GeometryChangedEvent>(FitRail);
            screen.UnregisterCallback<NavigationCancelEvent>(OnCancel);
            screen.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }
    }
}
