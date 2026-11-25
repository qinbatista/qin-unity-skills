using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    static class LayoutInput
    {
        public static void MoveFocus(VisualElement origin, NavigationMoveEvent.Direction direction)
        {
            using NavigationMoveEvent move = NavigationMoveEvent.GetPooled(direction);
            origin.SendEvent(move);
        }

        public static void SendTouch(IPanel panel, int finger, TouchPhase phase, Vector2 position)
        {
            Touch touch = new Touch { fingerId = finger, position = position, rawPosition = position, phase = phase, tapCount = 1 };
            int pointerId = PointerId.touchPointerIdBase + finger;
            VisualElement receiver = panel.GetCapturingElement(pointerId) as VisualElement ?? panel.Pick(position);
            Assert.That(receiver, Is.Not.Null, "panel receiver for " + phase);
            using EventBase evt = phase switch
            {
                TouchPhase.Began => PointerDownEvent.GetPooled(touch, EventModifiers.None),
                TouchPhase.Moved => PointerMoveEvent.GetPooled(touch, EventModifiers.None),
                TouchPhase.Canceled => PointerCancelEvent.GetPooled(touch, EventModifiers.None),
                _ => PointerUpEvent.GetPooled(touch, EventModifiers.None)
            };
            evt.target = receiver;
            receiver.SendEvent(evt);
        }
    }
}
