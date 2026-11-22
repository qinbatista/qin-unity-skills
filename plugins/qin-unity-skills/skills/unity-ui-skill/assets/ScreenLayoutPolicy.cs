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
            public string sideCameraPlacement;
            public string sideCameraAppearance;
            public string duoOuterHeader;
            public bool blendCompactCamera;
            public float cameraRingDiameterInHubs;
            // Version 1 fields remain readable for existing projects.
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
            if (policy == null || (policy.version != 1 && policy.version != 2) || policy.profiles == null || policy.profiles.Length == 0 ||
                policy.portraitReference.x <= 0 || policy.portraitReference.y <= 0 ||
                policy.landscapeReference.x <= 0 || policy.landscapeReference.y <= 0 ||
                policy.edgeActions == null ||
                (policy.version == 1 ? policy.edgeActions.order != "top-to-bottom" : policy.edgeActions.sideCameraPlacement != "shift-inward" ||
                    (policy.edgeActions.sideCameraAppearance != "black" && policy.edgeActions.sideCameraAppearance != "authored" &&
                     policy.edgeActions.sideCameraAppearance != "artwork-ring") ||
                    (policy.edgeActions.sideCameraAppearance == "artwork-ring" && !(policy.edgeActions.cameraRingDiameterInHubs > 1f))) ||
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

        // A known local side camera can explain the inset at this end of a top row.
        // Other known system exclusions must also be supplied to the placement pass.
        public Rect TopRowLane(Rect pane, Rect safe, IReadOnlyList<Rect> exclusions, bool rightEdge) =>
            TopRowLane(pane, safe, exclusions, rightEdge, out _);

        public Rect TopRowLane(Rect pane, Rect safe, IReadOnlyList<Rect> exclusions, bool rightEdge, out bool sideCamera)
        {
            sideCamera = false;
            Rect lane = Rect.MinMaxRect(Mathf.Max(pane.xMin, safe.xMin), Mathf.Max(pane.yMin, safe.yMin),
                Mathf.Min(pane.xMax, safe.xMax), Mathf.Min(pane.yMax, safe.yMax));
            if (lane.width <= 0 || lane.height <= 0) return Rect.zero;
            if (exclusions == null) return lane;
            foreach (Rect r in exclusions)
            {
                if (r.width <= 0 || r.height <= 0 || !pane.Overlaps(r) || r.yMax >= pane.center.y) continue;
                bool corner = rightEdge
                    ? r.xMin >= pane.center.x && pane.xMax - r.xMax <= Mathf.Max(r.width, r.height)
                    : r.xMax <= pane.center.x && r.xMin - pane.xMin <= Mathf.Max(r.width, r.height);
                if (!corner) continue;
                sideCamera = true;
                if (rightEdge && r.xMax > lane.xMax) lane.xMax = pane.xMax;
                if (!rightEdge && r.xMin < lane.xMin) lane.xMin = pane.xMin;
            }
            return lane;
        }

        // Only the native Duo outer-display adapter supplies an aperture. All inputs
        // are in the current window's top-left pixel space. The body keeps its own pane.
        public bool TryDuoOuterHeader(Rect pane, Rect safe, IReadOnlyList<Rect> exclusions, Rect aperture, out Rect header, out float centerY)
        {
            header = default;
            centerY = 0f;
            if (edgeActions.duoOuterHeader != "left-of-camera-align-centers" || aperture.width <= 0f || aperture.height <= 0f ||
                !pane.Overlaps(aperture) || aperture.xMin < pane.center.x || aperture.yMax > pane.center.y) return false;

            float left = Mathf.Max(pane.xMin, safe.xMin);
            float right = aperture.xMin;
            if (exclusions != null)
                foreach (Rect region in exclusions)
                {
                    if (region.width <= 0f || region.height <= 0f || !pane.Overlaps(region)) continue;
                    // The native island can extend beyond the black lens when expanded.
                    if (region.xMin >= pane.center.x && region.Overlaps(aperture))
                        right = Mathf.Min(right, region.xMin);
                }
            if (right <= left || safe.yMax <= pane.yMin) return false;
            header = Rect.MinMaxRect(left, pane.yMin, right, Mathf.Min(pane.yMax, safe.yMax));
            centerY = aperture.center.y;
            return true;
        }

        // A short corner obstruction does not consume a full-height body column.
        // Keep persistent side bars and the system's top and bottom boundaries.
        public Rect BodyAroundCorner(Rect pane, Rect safe, IReadOnlyList<Rect> exclusions, bool rightEdge)
        {
            Rect lane = TopRowLane(pane, safe, exclusions, rightEdge, out bool sideCamera);
            if (!sideCamera || lane.width <= 0) return safe;
            float edge = rightEdge ? safe.xMax : safe.xMin;
            foreach (Rect r in exclusions)
                if (r.width > 0 && r.height > 0 && pane.Overlaps(r) && r.yMax >= pane.center.y &&
                    (rightEdge ? r.xMax > edge : r.xMin < edge)) return safe;
            if (rightEdge) safe.xMax = lane.xMax;
            else safe.xMin = lane.xMin;
            return safe;
        }

        // A compact localized camera can share a black backing with an adjacent button.
        // A larger system region only displaces the button. The backing never owns input.
        public bool TryGetCameraCap(Rect pane, Rect button, IReadOnlyList<Rect> exclusions, bool rightEdge,
            out Rect cap)
            => TryGetCameraCap(pane, button, exclusions, rightEdge, out cap, out _);

        private bool TryGetCameraCap(Rect pane, Rect button, IReadOnlyList<Rect> exclusions, bool rightEdge,
            out Rect cap, out Rect reservation)
        {
            cap = reservation = default;
            if (!edgeActions.blendCompactCamera ||
                (edgeActions.sideCameraAppearance != "black" && edgeActions.sideCameraAppearance != "artwork-ring") ||
                button.width <= 0 || button.height <= 0 || exclusions == null) return false;
            float nearest = button.width * 2f;
            foreach (Rect r in exclusions)
            {
                float shortSide = Mathf.Min(r.width, r.height);
                if (shortSide <= 0 || Mathf.Max(r.width, r.height) > 1.26f * shortSide ||
                    shortSide > 1.4f * button.height || r.yMax > pane.center.y ||
                    r.yMin - pane.yMin > shortSide || !pane.Overlaps(r)) continue;
                if (rightEdge ? r.xMin < button.xMax || pane.xMax - r.xMax > shortSide
                              : r.xMax > button.xMin || r.xMin - pane.xMin > shortSide) continue;
                float distance = Vector2.Distance(button.center, r.center);
                if (distance > nearest) continue;
                nearest = distance;
                reservation = r;
                float diameter = shortSide * .9f;
                cap = new Rect(r.center - Vector2.one * diameter * .5f, Vector2.one * diameter);
            }
            return cap.width > 0;
        }

        public struct CameraRing
        {
            public Rect face, hub, reservation, clearTarget;

            public Rect InputBounds => Rect.MinMaxRect(Mathf.Min(face.xMin, clearTarget.xMin),
                Mathf.Min(face.yMin, clearTarget.yMin), Mathf.Max(face.xMax, clearTarget.xMax),
                Mathf.Max(face.yMax, clearTarget.yMax));

            public bool Accepts(Vector2 point)
            {
                if (reservation.Contains(point)) return false;
                return clearTarget.Contains(point) || (point - face.center).sqrMagnitude <= face.width * face.width * .25f;
            }
        }

        // Dress the static lens as the hub of a corner control. Retain a full-sized clear
        // tap target inward of the system reservation, as well as the exposed rim.
        public bool TryPlaceCameraRing(Rect pane, Rect lane, Rect button, IReadOnlyList<Rect> exclusions,
            float scale, bool rightEdge, out CameraRing ring)
        {
            ring = default;
            if (edgeActions.sideCameraAppearance != "artwork-ring" ||
                !TryGetCameraCap(pane, button, exclusions, rightEdge, out Rect hub, out Rect reserved)) return false;
            float gap = edgeActions.clearanceReferencePixels * scale;
            float radius = Mathf.Min(hub.width * edgeActions.cameraRingDiameterInHubs * .5f,
                Mathf.Min(Mathf.Min(hub.center.x - lane.xMin, lane.xMax - hub.center.x),
                    Mathf.Min(hub.center.y - lane.yMin, lane.yMax - hub.center.y)) - gap);
            // A tight top edge may leave no legible rim outside the system reservation.
            if (radius < Mathf.Max(reserved.width, reserved.height) * .5f + button.height * .15f) return false;
            Rect face = new Rect(hub.center - Vector2.one * radius, Vector2.one * (2f * radius));
            button.center = new Vector2(rightEdge ? reserved.xMin - gap - button.width * .5f :
                reserved.xMax + gap + button.width * .5f, hub.center.y);
            if (!TryPlaceInRow(lane, button, exclusions, scale, rightEdge, out Rect tap) ||
                !face.Overlaps(tap)) return false;
            foreach (Rect other in exclusions)
                if (other != reserved && other.width > 0 && other.height > 0 && face.Overlaps(other)) return false;
            ring = new CameraRing { face = face, hub = hub, reservation = reserved, clearTarget = tap };
            return true;
        }

        // Keep the authored Y and move a complete paint/hit envelope the least distance
        // away from its physical edge. Bound the lane at stationary peers on the same row.
        public bool TryPlaceInRow(Rect lane, Rect candidate, IReadOnlyList<Rect> exclusions, float scale,
            bool rightEdge, out Rect placed)
        {
            placed = candidate;
            if (scale <= 0 || candidate.width <= 0 || candidate.height <= 0 ||
                lane.width < candidate.width || candidate.yMin < lane.yMin || candidate.yMax > lane.yMax)
                return false;
            if (rightEdge ? candidate.xMin < lane.xMin : candidate.xMax > lane.xMax) return false;
            placed.x = Mathf.Clamp(placed.x, lane.xMin, lane.xMax - placed.width);
            float gap = edgeActions.clearanceReferencePixels * scale;
            for (int pass = 0; pass <= (exclusions?.Count ?? 0); pass++)
            {
                float next = placed.x;
                if (exclusions != null)
                    foreach (Rect r in exclusions)
                    {
                        if (r.width <= 0 || r.height <= 0 || placed.yMax <= r.yMin - gap ||
                            placed.yMin >= r.yMax + gap || placed.xMax <= r.xMin - gap ||
                            placed.xMin >= r.xMax + gap) continue;
                        next = rightEdge ? Mathf.Min(next, r.xMin - gap - placed.width)
                                         : Mathf.Max(next, r.xMax + gap);
                    }
                if (next == placed.x) return true;
                if (next < lane.xMin || next + placed.width > lane.xMax) return false;
                placed.x = next;
            }
            return false;
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
