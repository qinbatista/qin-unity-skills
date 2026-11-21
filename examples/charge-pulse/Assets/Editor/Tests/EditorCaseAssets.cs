using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Qin.UnitySamples.Tests
{
    sealed class EditorCaseAssets : IDisposable
    {
        public string Folder { get; }

        public EditorCaseAssets(string parent = "Assets/Editor")
        {
            string name = $"SkillCase-{Guid.NewGuid():N}";
            Assert.That(AssetDatabase.CreateFolder(parent, name), Is.Not.Empty);
            Folder = $"{parent}/{name}";
        }

        public string Write(string name, byte[] bytes)
        {
            string path = $"{Folder}/{name}";
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        public void Dispose()
        {
            Assert.That(AssetDatabase.DeleteAsset(Folder), Is.True, $"Failed to retire {Folder}.");
        }
    }
}
