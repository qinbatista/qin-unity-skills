#if SO_DATA_UNITY_NETCODE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Enable SO_DATA_UNITY_NETCODE in the assembly that owns the Netcode for GameObjects adapter.
[RequireComponent(typeof(NetworkObject))]
public sealed class SOUnityNetcodeStore : NetworkBehaviour, ISOMatchSessionStore
{
    [Tooltip("Default SO data scope. The server may set a profile-specific scope before spawning.")]
    [SerializeField] private string scope;

    private readonly NetworkVariable<FixedString512Bytes> networkScope = new NetworkVariable<FixedString512Bytes>();
    private NetworkList<Field> fields;
    private readonly Dictionary<FixedString64Bytes, Action<SODataRecord>> observers =
        new Dictionary<FixedString64Bytes, Action<SODataRecord>>();
    private int mainThread;
    private ulong spawnId;
    private ulong sequence;
    private bool delivering;

    private struct Field : INetworkSerializable, IEquatable<Field>
    {
        public FixedString64Bytes Key;
        public FixedString512Bytes Value;
        public ulong SpawnId;
        public ulong Sequence;

        public string Revision => SpawnId.ToString("x16", CultureInfo.InvariantCulture)
            + Sequence.ToString("x16", CultureInfo.InvariantCulture);
        public SODataRecord Record => new SODataRecord(Value.ToString(), Revision);
        public bool Equals(Field other) => SpawnId == other.SpawnId && Sequence == other.Sequence
            && Key.Equals(other.Key) && Value.Equals(other.Value);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Key);
            serializer.SerializeValue(ref Value);
            serializer.SerializeValue(ref SpawnId);
            serializer.SerializeValue(ref Sequence);
        }
    }

    private void Awake()
    {
        mainThread = Thread.CurrentThread.ManagedThreadId;
        fields = new NetworkList<Field>(null, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public void SetScopeBeforeSpawn(string storageScope)
    {
        if (IsSpawned) throw new InvalidOperationException("Set the storage scope before the server spawns this object.");
        ValidateScopeName(storageScope);
        scope = storageScope;
    }

    private static void ValidateScopeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) > FixedString512Bytes.UTF8MaxLengthInBytes)
            throw new ArgumentException("Configure a nonempty SO storage scope within the network string capacity.");
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ValidateScopeName(scope);
            networkScope.Value = new FixedString512Bytes(scope);
            // A pooled network object starts a new match state on each spawn.
            fields.Clear();
            spawnId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            sequence = 0;
        }
        fields.OnListChanged += OnFieldChanged;
    }

    public override void OnNetworkDespawn()
    {
        fields.OnListChanged -= OnFieldChanged;
        observers.Clear();
    }

    public Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation)
    {
        FixedString64Bytes name = Validate(scope, key, cancellation);
        return Task.FromResult(Current(name));
    }

    public Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision,
        CancellationToken cancellation)
    {
        FixedString64Bytes name = Validate(scope, key, cancellation);
        if (!IsServer) throw new UnauthorizedAccessException("Only the authoritative server writes match state.");
        if (delivering) throw new InvalidOperationException("Defer gameplay commands until the push callback returns.");
        if (value == null || Encoding.UTF8.GetByteCount(value) > FixedString512Bytes.UTF8MaxLengthInBytes)
            throw new ArgumentOutOfRangeException(nameof(value), "The value exceeds the network field capacity.");

        int index = FindField(name);
        if ((index < 0 ? null : fields[index].Revision) != expectedRevision)
            throw new SODataConflictException("The network field changed. Read its current revision.");
        if (index < 0 && fields.Count >= 64)
            throw new InvalidOperationException("A network data domain supports up to 64 fields.");
        Field next = new Field
        {
            Key = name, Value = new FixedString512Bytes(value),
            SpawnId = spawnId, Sequence = checked(++sequence)
        };
        if (index < 0) fields.Add(next);
        else fields[index] = next;
        return Task.FromResult(next.Record);
    }

    public Task<IDisposable> SubscribeAsync(string scope, string key, Action<SODataRecord> receive,
        CancellationToken cancellation)
    {
        FixedString64Bytes name = Validate(scope, key, cancellation);
        if (receive == null) throw new ArgumentNullException(nameof(receive));
        Subscription subscription = new Subscription(this, name, receive);
        observers.TryGetValue(name, out var existing);
        observers[name] = existing + subscription.Deliver;
        try { receive(Current(name)); }
        catch { subscription.Dispose(); throw; }
        return Task.FromResult<IDisposable>(subscription);
    }

    private FixedString64Bytes Validate(string requestedScope, string key, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        RequireMainThread();
        if (!IsSpawned || !NetworkManager.IsListening || NetworkManager.ShutdownInProgress || NetworkManager.DistributedAuthorityMode)
            throw new InvalidOperationException("Bind a spawned object in a listening client/server session.");
        ValidateScopeName(requestedScope);
        if (!networkScope.Value.Equals(new FixedString512Bytes(requestedScope)))
            throw new ArgumentException("Bind this SO to the scope selected by the server.", nameof(requestedScope));
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) > FixedString64Bytes.UTF8MaxLengthInBytes)
            throw new ArgumentException("Use a nonempty key that fits the network field name.", nameof(key));
        return new FixedString64Bytes(key);
    }

    private void RequireMainThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != mainThread)
            throw new InvalidOperationException("Use the NGO store on the Unity main thread.");
    }

    private int FindField(FixedString64Bytes name)
    {
        for (int i = 0; i < fields.Count; i++)
            if (fields[i].Key.Equals(name)) return i;
        return -1;
    }

    private SODataRecord Current(FixedString64Bytes name)
    {
        int index = FindField(name);
        return index < 0 ? null : fields[index].Record;
    }

    private void OnFieldChanged(NetworkListEvent<Field> change)
    {
        if (!observers.TryGetValue(change.Value.Key, out var notify)) return;
        delivering = true;
        try { notify(change.Value.Record); }
        finally { delivering = false; }
    }

    private sealed class Subscription : IDisposable
    {
        private SOUnityNetcodeStore owner;
        private readonly FixedString64Bytes key;
        private Action<SODataRecord> receive;

        public Subscription(SOUnityNetcodeStore owner, FixedString64Bytes key, Action<SODataRecord> receive)
        {
            this.owner = owner;
            this.key = key;
            this.receive = receive;
        }

        public void Deliver(SODataRecord record)
        {
            try { receive?.Invoke(record); }
            catch (Exception error) { Debug.LogException(error, owner); }
        }

        public void Dispose()
        {
            if (receive == null) return;
            owner.RequireMainThread();
            if (owner.observers.TryGetValue(key, out var callbacks))
            {
                callbacks -= Deliver;
                if (callbacks == null) owner.observers.Remove(key);
                else owner.observers[key] = callbacks;
            }
            receive = null;
            owner = null;
        }
    }
}
#endif
