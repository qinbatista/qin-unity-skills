using System;
using System.Collections.Generic;
using UnityEngine;

namespace Qin.UnityUI
{
    // Input rectangles use active-window pixels with a top-left origin.
    [Serializable]
    public sealed class ScreenLayoutPolicy
    {
        [Serializable]
        public sealed class Profile
        {
            public string id;
            public float maxWidthOverHeight;
            public string body;
            public string header;
        }

        [Serializable]
        public sealed class EdgeActions
        {
            public bool dockBelowSideCamera;
            public string order;
            public float clearanceReferencePixels;
        }

        [Serializable]
        public sealed class SeparatingFold
        {
            public string equalPanePreference;
        }

        public int version;
        public Vector2 portraitReference;
        public Vector2 landscapeReference;
        public Profile[] profiles;
        public EdgeActions edgeActions;
        public SeparatingFold separatingFold;

        public static ScreenLayoutPolicy Read(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var policy = JsonUtility.FromJson<ScreenLayoutPolicy>(asset.text);
            if (policy == null || policy.version != 1 || policy.profiles == null || policy.profiles.Length == 0 ||
                policy.portraitReference.x <= 0 || policy.portraitReference.y <= 0 ||
                policy.landscapeReference.x <= 0 || policy.landscapeReference.y <= 0 ||
                policy.edgeActions == null || policy.edgeActions.order != "top-to-bottom" ||
                policy.edgeActions.clearanceReferencePixels < 0 || policy.separatingFold == null ||
                (policy.separatingFold.equalPanePreference != "top-or-left" && policy.separatingFold.equalPanePreference != "bottom-or-right"))
                throw new ArgumentException("Invalid screen layout policy.", nameof(asset));

            float previous = 0;
            for (int i = 0; i < policy.profiles.Length; i++)
            {
                Profile profile = policy.profiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.id) || string.IsNullOrWhiteSpace(profile.body) ||
                    string.IsNullOrWhiteSpace(profile.header) ||
                    (i == policy.profiles.Length - 1 ? profile.maxWidthOverHeight != 0 : profile.maxWidthOverHeight <= previous))
                    throw new ArgumentException("Profiles need increasing aspect limits and a final unbounded profile.", nameof(asset));
                previous = profile.maxWidthOverHeight;
            }
            return policy;
        }

        public Profile Select(Rect pane)
        {
            if (pane.width <= 0 || pane.height <= 0) throw new ArgumentOutOfRangeException(nameof(pane));
            foreach (Profile profile in profiles)
                if (profile.maxWidthOverHeight == 0 || pane.width <= pane.height * profile.maxWidthOverHeight)
                    return profile;
            throw new InvalidOperationException("Missing unbounded profile.");
        }

        public float Scale(Rect pane)
        {
            Vector2 reference = pane.width > pane.height ? landscapeReference : portraitReference;
            return Mathf.Min(pane.width / reference.x, pane.height / reference.y);
        }

        // Call only for an explicitly separating span. A crease can be flat and usable.
        // A zero-width or zero-height separating line is valid. Preserve both panes for two-pane UIs.
        public bool TrySplit(Rect window, Rect divider, out Rect first, out Rect second)
        {
            first = second = window;
            if (divider.width < 0 || divider.height < 0) return false;
            if (divider.yMin <= window.yMin && divider.yMax >= window.yMax &&
                divider.xMin > window.xMin && divider.xMax < window.xMax)
            {
                first.xMax = divider.xMin;
                second.xMin = divider.xMax;
            }
            else if (divider.xMin <= window.xMin && divider.xMax >= window.xMax &&
                     divider.yMin > window.yMin && divider.yMax < window.yMax)
            {
                first.yMax = divider.yMin;
                second.yMin = divider.yMax;
            }
            else return false;
            return true;
        }

        public Rect PrimaryPane(Rect first, Rect second)
        {
            float a = first.width * first.height, b = second.width * second.height;
            if (a == b) return separatingFold.equalPanePreference == "top-or-left" ? first : second;
            return a > b ? first : second;
        }

        // Reuse only the part of a side inset explained by a localized top camera.
        // The caller supplies all known hardware and system exclusions for the current window.
        public Rect ActionLane(Rect pane, Rect safeArea, IReadOnlyList<Rect> exclusions, bool rightEdge)
        {
            float left = Mathf.Max(pane.xMin, safeArea.xMin);
            float right = Mathf.Min(pane.xMax, safeArea.xMax);
            float top = Mathf.Max(pane.yMin, safeArea.yMin);
            float bottom = Mathf.Min(pane.yMax, safeArea.yMax);
            if (right <= left || bottom <= top) return Rect.zero;

            if (edgeActions.dockBelowSideCamera && exclusions != null)
                foreach (Rect region in exclusions)
                {
                    if (region.width <= 0 || region.height <= 0 || region.yMin > top + 1 ||
                        region.yMax <= pane.yMin || region.yMax >= bottom) continue;
                    if (rightEdge && region.xMin >= pane.center.x && region.xMin <= right + 1 && region.xMax >= pane.xMax - 1)
                        right = pane.xMax;
                    if (!rightEdge && region.xMax <= pane.center.x && region.xMax >= left - 1 && region.xMin <= pane.xMin + 1)
                        left = pane.xMin;
                }
            return Rect.MinMaxRect(left, top, right, bottom);
        }

        // 'lane' already includes the relevant safe margins. The candidate contains painting and hits
        // for one action or the complete column. False asks the host to reflow or scroll the group.
        public bool TryPlaceBelow(Rect lane, Rect candidate, IReadOnlyList<Rect> occlusions, float scale, out Rect placed)
        {
            placed = candidate;
            if (scale <= 0 || candidate.width <= 0 || candidate.height <= 0 ||
                candidate.xMin < lane.xMin || candidate.xMax > lane.xMax) return false;
            placed.y = Mathf.Max(lane.yMin, placed.y);
            float gap = edgeActions.clearanceReferencePixels * scale;
            for (int pass = 0; pass <= (occlusions?.Count ?? 0); pass++)
            {
                if (placed.yMax > lane.yMax) return false;
                float next = placed.y;
                if (occlusions != null)
                    foreach (Rect region in occlusions)
                    {
                        if (region.width <= 0 || region.height <= 0) continue;
                        if (placed.xMin < region.xMax + gap && placed.xMax > region.xMin - gap &&
                            placed.yMin < region.yMax + gap && placed.yMax > region.yMin - gap)
                            next = Mathf.Max(next, region.yMax + gap);
                    }
                if (next == placed.y) return true;
                placed.y = next;
            }
            return false;
        }
    }
}
