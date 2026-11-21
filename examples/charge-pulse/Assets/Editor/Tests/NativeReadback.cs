using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    sealed class NativeReadback : IDisposable
    {
        private readonly Texture2D _image;

        public int BrightPixels { get; }
        public int CoveredPixels { get; }

        public NativeReadback(RenderTexture source)
        {
            _image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                _image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                _image.Apply();
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

            foreach (Color32 pixel in _image.GetPixels32())
            {
                if (pixel.g > 150) BrightPixels++;
                if (pixel.a > 127) CoveredPixels++;
            }
        }

        public Color32 CenterPixel => _image.GetPixel(_image.width / 2, _image.height / 2);

        public void SaveIfRequested(string name)
        {
            string path = Environment.GetEnvironmentVariable("CHARGE_PULSE_EVIDENCE");
            if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(Path.Combine(path, name), _image.EncodeToPNG());
        }

        public void Dispose()
        {
            if (Application.isPlaying) Object.Destroy(_image);
            else Object.DestroyImmediate(_image);
        }
    }
}
