using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    public sealed partial class ResponsiveLayoutCases
    {
        static IEnumerator CheckInventoryReorder(VisualElement screen, InventoryReorder reorder, VisualElement[] cards,
            LayoutProfile profile, LayoutText strings, RenderTexture target)
        {
            VisualElement grid = screen.Q("tiles");
            VisualElement rope = cards[0];
            VisualElement map = cards[1];
            Vector2 first = rope.worldBound.center;
            Vector2 second = map.worldBound.center;
            int before = reorder.Moves;
            string cell = profile.Name + "-" + strings.Locale;
            Assert.That(screen.panel.Pick(first), Is.SameAs(rope));
            Assert.That(screen.panel.Pick(second), Is.SameAs(map));
            string ropeLabel = rope.Q<Label>("item-name").text;

            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Began, first);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Moved, second);
            yield return null;
            Assert.That(grid.HasPointerCapture(PointerId.touchPointerIdBase), Is.True, cell);
            Assert.That(rope.ClassListContains("move-source"), Is.True, cell);
            Assert.That(map.ClassListContains("drop-target"), Is.True, cell);
            Assert.That(grid.ElementAt(0), Is.SameAs(rope), cell + " preserve order before drop");
            Assert.That(grid.ElementAt(1), Is.SameAs(map));
            AssertTextFits(screen.Q<Label>("destination"));
            if (profile.Name == "compact-portrait" && strings.Locale == "de-DE")
                using (NativeReadback image = new NativeReadback(target)) image.SaveIfRequested($"inventory-drag-{cell}.png");
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Ended, second);
            yield return null;
            Assert.That(grid.ElementAt(1), Is.SameAs(rope), cell + " drop on second slot");
            Assert.That(grid.ElementAt(0), Is.SameAs(map));
            Assert.That(rope.userData, Is.EqualTo("rope"));
            Assert.That(rope.Q<Label>("item-name").text, Is.EqualTo(ropeLabel));
            Assert.That(screen.focusController.focusedElement, Is.SameAs(rope), cell + " moved item focus");
            Assert.That(reorder.Moves, Is.EqualTo(before + 1), cell + " single move");

            // Dropping outside the grid and cancelling on an eligible target both keep the committed order.
            Vector2 outside = new Vector2(grid.worldBound.center.x, grid.worldBound.yMin - 6f);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Began, rope.worldBound.center);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Moved, outside);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Ended, outside);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Began, rope.worldBound.center);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Moved, map.worldBound.center);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Canceled, map.worldBound.center);
            Assert.That(grid.ElementAt(1), Is.SameAs(rope));
            Assert.That(grid.HasPointerCapture(PointerId.touchPointerIdBase), Is.False);
            Assert.That(screen.ClassListContains("inventory-moving"), Is.False);
            Assert.That(reorder.Moves, Is.EqualTo(before + 1));

            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Began, rope.worldBound.center);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Ended, rope.worldBound.center);
            Assert.That(rope.ClassListContains("move-source"), Is.True, cell + " tap selection");
            using (NavigationCancelEvent cancel = NavigationCancelEvent.GetPooled()) rope.SendEvent(cancel);
            Assert.That(screen.ClassListContains("inventory-moving"), Is.False, cell + " cancel selection");
            Assert.That(reorder.Moves, Is.EqualTo(before + 1));

            // The same transfer is available through two submits with native spatial navigation between them.
            using (NavigationSubmitEvent select = NavigationSubmitEvent.GetPooled()) rope.SendEvent(select);
            Assert.That(rope.ClassListContains("move-source"), Is.True);
            LayoutInput.MoveFocus(rope, NavigationMoveEvent.Direction.Left);
            yield return null;
            Assert.That(screen.focusController.focusedElement, Is.SameAs(map), cell + " previous slot");
            Assert.That(map.ClassListContains("drop-target"), Is.True);
            using (NavigationSubmitEvent accept = NavigationSubmitEvent.GetPooled()) map.SendEvent(accept);
            yield return null;
            Assert.That(reorder.Moves, Is.EqualTo(before + 2), cell + " single accessible transfer");
            Assert.That(grid.childCount, Is.EqualTo(cards.Length));
            for (int index = 0; index < cards.Length; index++) Assert.That(grid.ElementAt(index), Is.SameAs(cards[index]));
            Assert.That(screen.focusController.focusedElement, Is.SameAs(rope), cell + " focus follows the item");
        }
    }
}
