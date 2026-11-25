using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Qin.UnitySamples.Tests
{
    public sealed partial class ResponsiveLayoutCases
    {
        static LayoutProfile[] _selectedProfiles;

        static LayoutProfile[] Profiles => _selectedProfiles ??= SelectProfiles();

        static LayoutProfile[] SelectProfiles()
        {
            string family = Environment.GetEnvironmentVariable("CHARGE_PULSE_PROFILES");
            if (string.IsNullOrEmpty(family) || family == "base") return BaseProfiles;
            if (family != "iphone") throw new ArgumentException("Unknown CHARGE_PULSE_PROFILES value: " + family);

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Assets + "IPhoneProfiles.json");
            if (asset == null) throw new InvalidOperationException("Missing iPhone viewport profile table.");
            RecordedProfile[] records = JsonUtility.FromJson<RecordedProfileTable>(asset.text).profiles;
            if (records == null || records.Length == 0) throw new InvalidOperationException("The iPhone profile table is empty.");
            LayoutProfile[] profiles = new LayoutProfile[records.Length];
            HashSet<string> names = new HashSet<string>();
            for (int index = 0; index < records.Length; index++)
            {
                RecordedProfile record = records[index];
                if (string.IsNullOrWhiteSpace(record.name) || !names.Add(record.name) ||
                    (record.source != "simulator" && record.source != "unity-device-profile" && record.source != "synthetic") ||
                    record.pixels?.Length != 2 || record.pixels[0] <= 0 || record.pixels[1] <= 0 ||
                    (record.source == "simulator" && record.pixelsPerPoint <= 0f) ||
                    !Enum.TryParse(record.header, true, out HeaderMode header))
                    throw new ArgumentException("Invalid iPhone profile at index " + index);

                Rect viewport = new Rect(0, 0, record.pixels[0], record.pixels[1]);
                Rect safe = record.ReadRegion(record.safeTopLeft, viewport);
                Rect[] dividers = record.ReadRegions(record.divisions, viewport);
                Rect cutout = record.reservedTopLeft == null || record.reservedTopLeft.Length == 0
                    ? default : record.ReadRegion(record.reservedTopLeft, viewport);
                List<Rect> regions = new List<Rect>();
                if (cutout.width > 0f) regions.Add(cutout);
                regions.AddRange(record.ReadRegions(record.additionalReservations, viewport));
                Rect pane = SelectContinuousPane(safe, dividers);
                if (record.source == "simulator") pane = ClearSpanningEdges(pane, regions);
                if (record.expectedPaneTopLeft?.Length > 0 && pane != record.ReadRegion(record.expectedPaneTopLeft, viewport))
                    throw new InvalidOperationException("Foreground pane differs from the recorded expectation for " + record.name);
                regions.AddRange(dividers);
                profiles[index] = new LayoutProfile(record.name, record.pixels[0], record.pixels[1], pane, cutout, header,
                    record.checkMiddleOnTransfer, record.source == "synthetic", regions.ToArray(), record.pixelsPerPoint);
            }
            return profiles;
        }

        // A system strip may reach inside the safe rectangle. Only a strip spanning an entire
        // foreground edge trims that edge. Local camera regions stay local, and the backdrop stays full-screen.
        static Rect ClearSpanningEdges(Rect pane, List<Rect> regions)
        {
            foreach (Rect region in regions)
            {
                if (!pane.Overlaps(region)) continue;
                if (region.xMin <= pane.xMin && region.xMax >= pane.xMax)
                {
                    if (region.yMin <= pane.yMin && region.yMax < pane.yMax) pane.yMin = region.yMax;
                    else if (region.yMax >= pane.yMax && region.yMin > pane.yMin) pane.yMax = region.yMin;
                }
                else if (region.yMin <= pane.yMin && region.yMax >= pane.yMax)
                {
                    if (region.xMin <= pane.xMin && region.xMax < pane.xMax) pane.xMin = region.xMax;
                    else if (region.xMax >= pane.xMax && region.xMin > pane.xMin) pane.xMax = region.xMin;
                }
            }
            return pane;
        }

        // Match the native probe: a divider must span a continuous pane (within one framebuffer pixel).
        // Keep the top/left candidate for an exact area tie.
        static Rect SelectContinuousPane(Rect safe, Rect[] dividers)
        {
            if (dividers.Length == 0) return safe;
            List<Rect> panes = new List<Rect> { safe };
            foreach (Rect divider in dividers)
            {
                List<Rect> next = new List<Rect>();
                foreach (Rect pane in panes)
                {
                    Rect first = pane;
                    Rect second = pane;
                    if (divider.yMin <= pane.yMin + 1f && divider.yMax >= pane.yMax - 1f && divider.xMin > pane.xMin && divider.xMax < pane.xMax)
                    {
                        first.xMax = divider.xMin;
                        second.xMin = divider.xMax;
                    }
                    else if (divider.xMin <= pane.xMin + 1f && divider.xMax >= pane.xMax - 1f && divider.yMin > pane.yMin && divider.yMax < pane.yMax)
                    {
                        first.yMax = divider.yMin;
                        second.yMin = divider.yMax;
                    }
                    else
                    {
                        next.Add(pane);
                        continue;
                    }
                    next.Add(first);
                    next.Add(second);
                }
                panes = next;
            }
            Rect primary = panes[0];
            foreach (Rect pane in panes)
                if (pane.width * pane.height > primary.width * primary.height) primary = pane;
            return primary;
        }

        [Serializable]
        sealed class RecordedProfileTable
        {
            public RecordedProfile[] profiles = Array.Empty<RecordedProfile>();
        }

        [Serializable]
        sealed class RecordedRegion
        {
            public int[] pixels = Array.Empty<int>();
        }

        [Serializable]
        sealed class RecordedProfile
        {
            public string name = "";
            public string source = "";
            public string header = "";
            public bool checkMiddleOnTransfer = false;
            public int[] pixels = Array.Empty<int>();
            public float pixelsPerPoint = 0f;
            public int[] safeTopLeft = Array.Empty<int>();
            public int[] reservedTopLeft = Array.Empty<int>();
            public RecordedRegion[] additionalReservations = Array.Empty<RecordedRegion>();
            public RecordedRegion[] divisions = Array.Empty<RecordedRegion>();
            public int[] expectedPaneTopLeft = Array.Empty<int>();

            public Rect[] ReadRegions(RecordedRegion[] regions, Rect viewport) =>
                Array.ConvertAll(regions ?? Array.Empty<RecordedRegion>(), region => ReadRegion(region.pixels, viewport));

            public Rect ReadRegion(int[] values, Rect viewport)
            {
                if (values == null || values.Length != 4 || values[0] < 0 || values[1] < 0 ||
                    values[2] <= 0 || values[3] <= 0 || values[0] + values[2] > viewport.width || values[1] + values[3] > viewport.height)
                    throw new ArgumentException("Invalid top-left pixel rectangle for " + name);
                return new Rect(values[0], values[1], values[2], values[3]);
            }
        }
    }
}
