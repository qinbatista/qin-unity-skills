using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    sealed class NativeReadback : IDisposable
    {
        // Consumers borrow this image until the readback is disposed.
        public Texture2D CapturedTexture { get; }
        public int BrightPixels { get; }
        public int CoveredPixels { get; }

        public NativeReadback(RenderTexture source)
        {
            CapturedTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                CapturedTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                CapturedTexture.Apply();
            }
            catch
            {
                Dispose();
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
            }

            foreach (Color32 pixel in CapturedTexture.GetPixels32())
            {
                if (pixel.g > 150) BrightPixels++;
                if (pixel.a > 127) CoveredPixels++;
            }
        }

        public Color32 CenterPixel => CapturedTexture.GetPixel(CapturedTexture.width / 2, CapturedTexture.height / 2);

        public void SaveIfRequested(string name)
        {
            string path = Environment.GetEnvironmentVariable("CHARGE_PULSE_EVIDENCE");
            if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(Path.Combine(path, name), CapturedTexture.EncodeToPNG());
        }

        public void Dispose()
        {
            if (Application.isPlaying) Object.Destroy(CapturedTexture);
            else Object.DestroyImmediate(CapturedTexture);
        }
    }
}
