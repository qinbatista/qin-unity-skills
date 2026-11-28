# ScriptableObject property storage

## Choose one mode per domain

Inherit `SODataController` and choose one Inspector mode for the whole SO. Keep wallet, outfit and match state in separate assets. A regular ScriptableObject can still be used independently.

| Mode | What a property assignment does | Source |
| --- | --- | --- |
| None (default) | Changes the shared runtime instance in memory. | [Memory](../assets/Modes/SONoneMode.cs) |
| LocalFile | Commits the whole domain in one encrypted snapshot. | [Snapshot](../assets/Modes/SOLocalFileMode.cs) |
| DirectAccess | Commits only the changed key to its own encrypted native record before the setter returns. | [Offline keys](../assets/Modes/SODirectAccessMode.cs) |
| DirectAccessOnline | Changes the local view and queues a conditional write for this field on the signed-in account. | [Cloud mode](../assets/Modes/SODirectAccessOnlineMode.cs) |
| RealtimeOnline | Publishes an authorized field change and applies ordered remote updates to the local backing field. | [Live mode](../assets/Modes/SORealtimeOnlineMode.cs) |

## Copy and call the template

Copy [SODataController.cs](../assets/SODataController.cs) and the five files in [Modes](../assets/Modes). Add Unity's `com.unity.nuget.newtonsoft-json` package (tested with 3.2.2) and reference `Unity.Newtonsoft.Json` in the receiving assembly definition. Include only the optional channel your project uses.

Give each stored member a private serialized backing field and a C# property. The normal assignment calls the setter, which calls the selected mode. The base matches the property name to the backing field's key, trimming leading underscores and accepting the PascalCase property equivalent. For different names pass the explicit key to `Write`. Use `[SODataKey("old-key")]` to preserve a published key. Keep a renamed Unity backing field compatible with old authored assets using `FormerlySerializedAs`. Unity serializes the backing fields. Make the changes through their properties. To save a collection or nested record, assign the updated value back through the property.

```csharp
// SOPickupData.cs
public sealed class SOPickupData : SODataController
{
    [SerializeField, FormerlySerializedAs("value")] private int _value;
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int value { get => Read(ref _value); set => Write(ref _value, value); }
}
```

Ordinary gameplay uses `pickupData.value = 5;` and `int current = pickupData.value;`. The default offline slot opens on first access. For a named slot, call `await data.OpenAsync(slot)` before gameplay. After authentication or network spawn open an online SO once with `await data.OpenAsync(slot, channel)`. Use the same instance for its writers and readers. Await `data.CloseAsync()` before switching account, slot or match. It waits for pending acknowledgements and restores the authored defaults. `CloseData()` immediately detaches a retiring instance.

## Persistence and channels

LocalFile and DirectAccess retain the authenticated `SODATA/1` envelope, atomic replacement, device key and published paths. The envelope uses AES encryption, HMAC authentication, an MD5 payload checksum and Base64 encoding. Back up the device key with its files. A legacy LocalFile domain can override `LegacyFileName` for its original default-slot path. Opening a valid older file keeps its bytes intact until a property changes. Keep the same slot, storage ID and field keys when adopting an existing game. Keyed payloads retain their existing `{"value":5}` representation. The built-in file writer targets native mobile and desktop filesystems.

For another persistent channel implement `ISODataKeyStore` in one file. For a subscription channel implement `ISORealtimeDataStore` with initial state, ordered updates and unsubscribe. Return revisions, reject stale writes and surface errors. The SO exposes `DataChanged` after acceptance, `StorageFailed` on a failed online write, and `FlushAsync()` to await pending receipts. A lost reply requires a fresh read before retry. [Unity Cloud Save](so-unity-cloud-save.md) · [Unity Netcode](so-unity-netcode.md)
