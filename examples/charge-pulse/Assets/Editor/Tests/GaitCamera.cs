using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    // Fixed orthographic side view. Floor markings are spaced at one support-relative metre.
    sealed class GaitCamera : IDisposable
    {
        readonly string folder;
        readonly Transform stage, surface;
        readonly Camera camera;
        readonly RenderTexture target;
        readonly Texture2D pixels;
        readonly Material floor, markings;

        public GaitCamera(Transform support, string output, float courseLength)
        {
            folder = output;
            if (folder != null) Directory.CreateDirectory(folder);
            stage = VirtualWalker.Node("Gait capture", null, Vector3.zero);
            Transform mount = VirtualWalker.Node("Fixed camera", stage, new Vector3(courseLength * 0.5f, 1.12f, -10f));
            camera = mount.gameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << VirtualWalker.Layer;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1.8f, (courseLength + 2f) / 6f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(222, 232, 230, 255);
            target = new RenderTexture(720, 240, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            target.Create();
            camera.targetTexture = target;
            if (folder != null) pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            floor = new Material(Shader.Find("Unlit/Color")) { color = new Color32(47, 70, 78, 255) };
            markings = new Material(Shader.Find("Unlit/Color")) { color = new Color32(149, 177, 173, 255) };
            surface = VirtualWalker.Node("Marked ground", support, Vector3.zero);
            VirtualWalker.Shape("Surface", surface, PrimitiveType.Cube, new Vector3(4f, -0.23f, 0.12f), new Vector3(28f, 0.46f, 1.2f), floor);
            for (int metre = -2; metre <= 12; metre++)
                VirtualWalker.Shape("Metre", surface, PrimitiveType.Cube, new Vector3(metre, -0.08f, -0.50f), new Vector3(0.035f, 0.16f, 0.03f), markings);
        }

        public void Capture(int frame)
        {
            RenderTexture before = RenderTexture.active;
            try
            {
                camera.Render();
                if (folder == null) return;
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, $"{frame:D4}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = before;
            }
        }

        public void Dispose()
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(surface.gameObject);
            Object.DestroyImmediate(stage.gameObject);
            Object.DestroyImmediate(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(markings);
        }
    }
}
