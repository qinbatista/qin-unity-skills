using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Qin.UnityPerformance.Cases
{
    public sealed class AddressableCases
    {
        private const string Key = "PerformanceCases/Actor";
        private const string ActorPath = "Assets/PerformanceLeaseActor.prefab";
        private const string Marker = "Authored marker";
        private readonly List<Action> _remainingReleases = new List<Action>();
        private readonly List<GameObject> _clones = new List<GameObject>();

        private Action Own(Action release)
        {
            _remainingReleases.Add(release);
            return () =>
            {
                if (_remainingReleases.Remove(release)) release();
            };
        }

        private Action OwnInstance(AsyncOperationHandle<GameObject> handle)
        {
            return Own(() =>
            {
                if (handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded)
                    Assert.IsTrue(Addressables.ReleaseInstance(handle));
                else Addressables.Release(handle);
            });
        }

        // The replay runner owns a fresh project and calls this before entering Play Mode.
        public static void Configure()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath))
            {
                GameObject source = new GameObject("Performance lease actor");
                try
                {
                    new GameObject(Marker).transform.SetParent(source.transform);
                    source.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(source, ActorPath);
                }
                finally
                {
                    Object.DestroyImmediate(source);
                }
            }
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(ActorPath), settings.DefaultGroup);
            entry.address = Key;
            int fastMode = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
            Assert.That(fastMode, Is.GreaterThanOrEqualTo(0), "This replay uses the Editor AssetDatabase provider.");
            settings.ActivePlayModeDataBuilderIndex = fastMode;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        [UnitySetUp]
        public IEnumerator EnterFixture()
        {
            if (!Application.isPlaying) Configure();
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeaveFixture()
        {
            if (!Application.isPlaying) yield break;
            foreach (GameObject clone in _clones)
                if (clone != null) Object.Destroy(clone);
            _clones.Clear();
            yield return null;
            for (int i = _remainingReleases.Count - 1; i >= 0; i--) _remainingReleases[i]();
            _remainingReleases.Clear();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ColdLoadAndWarmReuseHoldDependenciesThroughLastClone()
        {
            Stopwatch clock = Stopwatch.StartNew();
            AsyncOperationHandle<GameObject> firstLoad = Addressables.LoadAssetAsync<GameObject>(Key);
            Action releaseFirst = Own(() => Addressables.Release(firstLoad));
            yield return firstLoad;
            TimeSpan firstLoadElapsed = clock.Elapsed;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, firstLoad.Status);

            clock.Restart();
            AsyncOperationHandle<GameObject> warmLoad = Addressables.LoadAssetAsync<GameObject>(Key);
            Action releaseWarm = Own(() => Addressables.Release(warmLoad));
            yield return warmLoad;
            TimeSpan warmLoadElapsed = clock.Elapsed;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, warmLoad.Status);
            Assert.AreSame(firstLoad.Result, warmLoad.Result);
            releaseWarm();
            Assert.IsTrue(firstLoad.IsValid());

            clock.Restart();
            GameObject active = Object.Instantiate(firstLoad.Result);
            _clones.Add(active);
            GameObject idle = Object.Instantiate(firstLoad.Result);
            _clones.Add(idle);
            TimeSpan cloneElapsed = clock.Elapsed;
            Assert.IsFalse(active.activeSelf);
            Assert.IsFalse(idle.activeSelf);
            clock.Restart();
            active.SetActive(true);
            TimeSpan activationElapsed = clock.Elapsed;
            Assert.IsTrue(active.activeInHierarchy);
            Object.Destroy(active);
            yield return null;
            Assert.IsTrue(active == null);
            Assert.IsTrue(firstLoad.IsValid());
            Assert.IsNotNull(idle.transform.Find(Marker));
            idle.SetActive(true);
            Assert.IsTrue(idle.activeInHierarchy);
            Object.Destroy(idle);
            yield return null;
            Assert.IsTrue(idle == null);
            releaseFirst();
            yield return null;
            Assert.IsFalse(firstLoad.IsValid());
            TestContext.WriteLine($"QIN_CASE Editor Play Mode, first keyed load={firstLoadElapsed.TotalMilliseconds:F3} ms, warm keyed load={warmLoadElapsed.TotalMilliseconds:F3} ms, clone two={cloneElapsed.TotalMilliseconds:F3} ms, activate one={activationElapsed.TotalMilliseconds:F3} ms; 2 acquisitions returned the same asset, 2 clones retired before final release");
        }

        [UnityTest]
        public IEnumerator InstantiatedPeersRetainIndependentLeases()
        {
            var locations = Addressables.LoadResourceLocationsAsync(Key, typeof(GameObject));
            Action releaseLocations = Own(() => Addressables.Release(locations));
            yield return locations;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, locations.Status);
            Assert.AreEqual(1, locations.Result.Count);
            string provider = locations.Result[0].ProviderId;
            Assert.That(provider, Does.Contain("AssetDatabaseProvider"));
            releaseLocations();

            AsyncOperationHandle<GameObject> first = Addressables.InstantiateAsync(Key);
            Action releaseFirst = OwnInstance(first);
            AsyncOperationHandle<GameObject> second = Addressables.InstantiateAsync(Key);
            Action releaseSecond = OwnInstance(second);
            yield return first;
            yield return second;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, first.Status);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, second.Status);
            GameObject a = first.Result;
            GameObject b = second.Result;
            Assert.AreNotSame(a, b);
            a.SetActive(true);
            releaseFirst();
            yield return null;
            Assert.IsTrue(a == null);
            Assert.IsFalse(first.IsValid());
            Assert.IsTrue(second.IsValid());
            Assert.IsFalse(b.activeSelf);
            Assert.IsNotNull(b.transform.Find(Marker));
            b.SetActive(true);
            Assert.IsTrue(b.activeInHierarchy);
            releaseSecond();
            yield return null;
            Assert.IsTrue(b == null);
            Assert.IsFalse(second.IsValid());
            TestContext.WriteLine($"QIN_CASE provider={provider}; instances=2, independent retirements=2, idle peer reused");
        }

        [UnityTest]
        public IEnumerator ClosedRequestReleasesLateArrivalAndMissingKeyCanRetry()
        {
            var locations = Addressables.LoadResourceLocationsAsync(Key, typeof(GameObject));
            Action releaseLocations = Own(() => Addressables.Release(locations));
            yield return locations;
            Assert.AreEqual(1, locations.Result.Count);
            const string retryKey = "PerformanceCases/AppearsAfterCatalogRefresh";
            ResourceLocationMap updatedLocations = new ResourceLocationMap("Performance retry catalog", 1);
            updatedLocations.Add(retryKey, locations.Result[0]);
            var previousHandler = ResourceManager.ExceptionHandler;
            int missingErrors = 0;
            ResourceManager.ExceptionHandler = (operation, error) =>
            {
                if (error is InvalidKeyException && error.Message.Contains(retryKey)) missingErrors++;
                else previousHandler?.Invoke(operation, error);
            };
            try
            {
                AsyncOperationHandle<GameObject> failed = Addressables.LoadAssetAsync<GameObject>(retryKey);
                Action releaseFailed = Own(() => Addressables.Release(failed));
                yield return failed;
                Assert.AreEqual(AsyncOperationStatus.Failed, failed.Status);
                Assert.That(failed.OperationException, Is.TypeOf<InvalidKeyException>());
                Assert.Greater(missingErrors, 0);
                releaseFailed();
                yield return null;
                Assert.IsFalse(failed.IsValid());
            }
            finally
            {
                ResourceManager.ExceptionHandler = previousHandler;
            }

            Addressables.AddResourceLocator(updatedLocations);
            try
            {
                AsyncOperationHandle<GameObject> retry = Addressables.LoadAssetAsync<GameObject>(retryKey);
                Action releaseRetry = Own(() => Addressables.Release(retry));
                yield return retry;
                Assert.AreEqual(AsyncOperationStatus.Succeeded, retry.Status);
                Assert.IsNotNull(retry.Result.transform.Find(Marker));
                releaseRetry();
            }
            finally
            {
                Addressables.RemoveResourceLocator(updatedLocations);
                releaseLocations();
            }

            int generation = 1;
            int pendingGeneration = generation;
            int staleReleases = 0;
            GameObject discarded = null;
            GameObject visible = null;
            AsyncOperationHandle<GameObject> oldRequest = Addressables.InstantiateAsync(Key);
            Action releaseOld = OwnInstance(oldRequest);
            oldRequest.Completed += operation =>
            {
                if (pendingGeneration == generation) visible = operation.Result;
                else
                {
                    discarded = operation.Result;
                    releaseOld();
                    staleReleases++;
                }
            };
            generation++; // Close the old view before its completion callback publishes.
            AsyncOperationHandle<GameObject> current = Addressables.InstantiateAsync(Key);
            Action releaseCurrent = OwnInstance(current);
            yield return current;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, current.Status);
            Assert.IsNull(visible, "Closing before completion suppresses the old publication.");
            visible = current.Result;
            visible.SetActive(true);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (staleReleases == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return null;
            Assert.AreEqual(1, staleReleases);
            Assert.IsFalse(oldRequest.IsValid());
            Assert.IsFalse(ReferenceEquals(discarded, null));
            Assert.IsTrue(discarded == null);
            Assert.AreSame(current.Result, visible);
            Assert.IsTrue(visible.activeInHierarchy);
            releaseCurrent();
            yield return null;
            Assert.IsTrue(visible == null);
            TestContext.WriteLine("QIN_CASE same missing key retried after catalog registration; stale instance released once, replacement stayed active through old completion");
        }
    }

    public sealed class BuiltContentCases
    {
        private const string Hard = "perf-hard-catalog";
        private const string Light = "perf-light-catalog";
        private const string Selected = "perf-selected-actor";
        private const string Optional = "perf-optional-mesh";
        private const string Source = "Assets/PerformanceBundleFixture";
        private const string MeshPath = Source + "/Optional.asset";
        private const string ActorPath = Source + "/Actor.prefab";
        private const string HardPath = Source + "/Hard.prefab";
        private const string LightPath = Source + "/Light.prefab";

        // The first host-target bundle build includes Unity's cold toolchain initialization.
        [Timeout(600_000)]
        [UnityTest]
        public IEnumerator LightweightCatalogLeavesOptionalBundleUnloadedThroughReentry()
        {
            if (!Application.isPlaying) BuildContent();
            yield return new EnterPlayMode();
            yield return ReplayContent();
        }

        private static string OutputDirectory =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "PerformanceBundleOutput");

        private static void BuildContent()
        {
            if (!AssetDatabase.IsValidFolder(Source)) AssetDatabase.CreateFolder("Assets", "PerformanceBundleFixture");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Optional geometry",
                    vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                    triangles = new[] { 0, 1, 2 }
                };
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            SavePrefab(ActorPath, null, false);
            SavePrefab(HardPath, mesh, true);
            SavePrefab(LightPath, null, true);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(OutputDirectory);
            AssetBundleBuild[] builds = new[]
            {
                new AssetBundleBuild { assetBundleName = Hard, assetNames = new[] { HardPath } },
                new AssetBundleBuild { assetBundleName = Light, assetNames = new[] { LightPath } },
                new AssetBundleBuild { assetBundleName = Selected, assetNames = new[] { ActorPath } },
                new AssetBundleBuild { assetBundleName = Optional, assetNames = new[] { MeshPath } }
            };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(OutputDirectory, builds,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                EditorUserBuildSettings.activeBuildTarget);
            Assert.IsNotNull(manifest, "The standalone AssetBundle content build failed.");
            CollectionAssert.AreEquivalent(new[] { Optional }, manifest.GetAllDependencies(Hard));
            Assert.IsEmpty(manifest.GetAllDependencies(Light));
            Assert.IsEmpty(manifest.GetAllDependencies(Selected));
        }

        private static void SavePrefab(string path, Mesh optionalMesh, bool catalog)
        {
            GameObject source = new GameObject(catalog ? "Actor catalog" : "Actor");
            try
            {
                if (catalog)
                {
                    new GameObject(Selected).transform.SetParent(source.transform);
                    new GameObject(Optional).transform.SetParent(source.transform);
                    if (optionalMesh != null) source.AddComponent<MeshFilter>().sharedMesh = optionalMesh;
                }
                else
                {
                    new GameObject("Actor marker").transform.SetParent(source.transform);
                    source.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(source, path);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static IEnumerator ReplayContent()
        {
            List<AssetBundle> open = new List<AssetBundle>();
            AssetBundle Own(string name)
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(OutputDirectory, name));
                Assert.IsNotNull(bundle, name);
                open.Add(bundle);
                return bundle;
            }
            void Unload()
            {
                for (int i = open.Count - 1; i >= 0; i--) open[i].Unload(true);
                open.Clear();
            }
            string[] Loaded() => AssetBundle.GetAllLoadedAssetBundles()
                .Select(bundle => bundle.name).Where(name => name == Hard || name == Light ||
                    name == Optional || name == Selected).ToArray();

            Assert.IsEmpty(Loaded());
            GameObject instance = null;
            try
            {
                Mesh optional = Own(Optional).LoadAsset<Mesh>(MeshPath);
                GameObject hardCatalog = Own(Hard).LoadAsset<GameObject>(HardPath);
                Assert.AreEqual(3, optional.vertexCount);
                Assert.AreSame(optional, hardCatalog.GetComponent<MeshFilter>().sharedMesh);
                CollectionAssert.AreEquivalent(new[] { Hard, Optional }, Loaded());
                Unload();
                Assert.IsEmpty(Loaded());

                for (int visit = 0; visit < 2; visit++)
                {
                    Stopwatch clock = Stopwatch.StartNew();
                    GameObject catalog = Own(Light).LoadAsset<GameObject>(LightPath);
                    Assert.AreEqual(Optional, catalog.transform.Find(Optional).name);
                    CollectionAssert.AreEquivalent(new[] { Light }, Loaded());
                    string selectedKey = catalog.transform.Find(Selected).name;
                    AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(Path.Combine(OutputDirectory, selectedKey));
                    yield return request;
                    AssetBundle actorBundle = request.assetBundle;
                    Assert.IsNotNull(actorBundle);
                    open.Add(actorBundle);
                    AssetBundleRequest prefabRequest = actorBundle.LoadAssetAsync<GameObject>(ActorPath);
                    yield return prefabRequest;
                    GameObject prefab = (GameObject)prefabRequest.asset;
                    Assert.IsNotNull(prefab);
                    TimeSpan loadElapsed = clock.Elapsed;
                    clock.Restart();
                    instance = Object.Instantiate(prefab);
                    Assert.IsFalse(instance.activeSelf);
                    instance.SetActive(true);
                    TimeSpan activationElapsed = clock.Elapsed;
                    Assert.IsTrue(instance.transform.Find("Actor marker").gameObject.activeInHierarchy);
                    CollectionAssert.AreEquivalent(new[] { Light, Selected }, Loaded());
                    Object.Destroy(instance);
                    yield return null;
                    Assert.IsTrue(instance == null);
                    Unload();
                    Assert.IsEmpty(Loaded());
                    TestContext.WriteLine($"QIN_CASE native bundle visit={visit + 1}, local catalog and selected prefab load={loadElapsed.TotalMilliseconds:F3} ms, instantiate/activate={activationElapsed.TotalMilliseconds:F3} ms, hard optional dependencies=1, light optional dependencies=0, loaded at return=0");
                }
            }
            finally
            {
                if (instance != null) Object.Destroy(instance);
                Unload();
            }
        }

        [UnityTearDown]
        public IEnumerator LeaveFixture()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
