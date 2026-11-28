#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SOPickupWalkthrough
{
    [MenuItem("Tools/SODataController/Run Pickup Walkthrough")]
    private static async void Run()
    {
        SOPickupData data = ScriptableObject.CreateInstance<SOPickupData>();
        SOPickupData reopened = null;
        GameObject host = EditorUtility.CreateGameObjectWithHideFlags("Pickup walkthrough", HideFlags.HideAndDontSave, typeof(PickupController));
        try
        {
            PickupController pickup = host.GetComponent<PickupController>();
            using (SerializedObject binding = new SerializedObject(pickup))
            {
                binding.FindProperty("_pickupData").objectReferenceValue = data;
                binding.ApplyModifiedPropertiesWithoutUndo();
            }

            WalkthroughStore store = new WalkthroughStore();
            await data.OpenAsync("demo", store);
            Debug.Log($"[SO pickup] 1. Opened {data.Mode}, value={data.value}");
            int confirmations = 0;
            data.DataChanged += key =>
            {
                confirmations++;
                Debug.Log($"[SO pickup] 6. DataChanged({key}), confirmed value={data.value}");
            };

            Debug.Log("[SO pickup] 2. Calling PickupController.OnPickup(), which assigns _pickupData.value = 5");
            pickup.OnPickup();
            Debug.Log($"[SO pickup] 4. Getter reads {data.value}, confirmations={confirmations}");
            pickup.OnPickup();
            Task close = data.CloseAsync();
            Debug.Log($"[SO pickup] 5. Assigned 5 again, writes={store.Writes}, close pending={!close.IsCompleted}");
            store.Confirm();
            await close;

            reopened = ScriptableObject.CreateInstance<SOPickupData>();
            await reopened.OpenAsync("demo", store);
            Debug.Log($"[SO pickup] 7. Fresh SO opened the same slot, value={reopened.value}");
            await reopened.CloseAsync();
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(data);
            if (reopened != null) Object.DestroyImmediate(reopened);
        }
    }

    // One example record and one delayed reply. Each run gets its own simulated account.
    private sealed class WalkthroughStore : ISODataKeyStore
    {
        private SODataRecord _saved = new SODataRecord("{\"value\":37}", "1");
        private readonly TaskCompletionSource<SODataRecord> _reply = new TaskCompletionSource<SODataRecord>();
        private string _submitted;
        public int Writes { get; private set; }

        public Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return Task.FromResult(_saved);
        }

        public Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_saved.Revision != expectedRevision) throw new SODataConflictException("The example revision changed.");
            Writes++;
            _submitted = value;
            Debug.Log($"[SO pickup] 3. Channel received scope={scope}, key={key}, payload={value}, revision={expectedRevision}");
            return _reply.Task;
        }

        public void Confirm()
        {
            _saved = new SODataRecord(_submitted, "2");
            _reply.SetResult(_saved);
        }
    }
}
#endif
