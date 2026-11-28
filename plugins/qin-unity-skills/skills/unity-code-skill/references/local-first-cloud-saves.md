# Local-first ScriptableObject cloud saves

## Implement a local-first save

Use this when the local save is authoritative and online backup or restore is part of its contract. First [choose the owner for server data](so-data-controller.md#choose-the-owner-for-server-data). Keep paid balances, purchase grants, and other server-owned data under their [authoritative transaction contract](unity-service-integration.md#purchase-and-durable-side-effects). Share their accepted values with gameplay through that contract.

### Own one save per domain

Extend the game's current local save and its existing gameplay entry. For a new snapshot domain, use the [LocalFile mode](so-data-controller.md#choose-one-mode-per-domain) and group values that must survive the same gameplay checkpoint. In a ScriptableObject domain the authored `.asset` supplies defaults and stable asset references. One runtime instance serves the active profile or save slot in both local-only and cloud-enabled configurations. [Existing local file base](unity-lifecycle-and-serialization.md#scriptableobject-local-files)

For optional backup of a LocalFile domain, synchronize the validated inner versioned JSON through the adapter. Encryption keys and the device-specific envelope stay on the device. For online-authoritative keys or realtime game state choose the appropriate [SODataController mode](so-data-controller.md#choose-one-mode-per-domain).

Represent an explicit, versioned data record as UTF-8 JSON under `Application.persistentDataPath`. Scope files to the game's profile and slot and preserve shipped names, schemas and migrations. Keep references as stable IDs and persist only declared gameplay values. Use the receiving project's serializer for lists and dictionaries and verify round trips on its supported Unity versions. Where the serializer cannot round-trip a dictionary, persist stable ID/value rows and build the lookup in memory. Follow the [ScriptableObject data checks](scriptableobject-data-contracts.md#design-and-review-a-data-owner).

### Commit locally, then synchronize

1. At startup, initialize the runtime instance and read its local JSON. Initialize and persist defaults for a genuinely new profile at the game's first-use checkpoint. A corrupt or unsupported file stays available for recovery. Local startup and checkpoints work offline.
2. For a new LocalFile domain, change the shared properties in memory and use its five-minute dirty checkpoint. Await `FlushAsync()` for an earlier save. Subscribe the file backup once to `SnapshotCommitted` and use its committed JSON string for the upload. For an existing save owner, use its own commit boundary. Publish a save completion only after the local commit succeeds. Let the game's synchronization owner observe successful gameplay commits and capture the immutable validated payload. Serialize the SO on the Unity thread and hand plain data or bytes to the I/O owner.
3. Check for remote progress at the game's restore boundary, such as authenticated startup or explicit restore. Require a matching remote revision or confirmed missing result before the first upload for a slot. Validate downloaded data with the local reader. When the account and local generation still match and there are no unsynced local edits, commit the validated file through the same local save owner and apply it to the runtime instance. Record the restore with its acknowledged content and remote revision.
4. For the configured, authenticated account, upload the latest committed JSON as one snapshot. Allow one in-flight upload per slot, coalesce repeated saves and skip unchanged gameplay content, including timestamp-only writes. Durably track the last acknowledged content and corresponding remote revision so pending local changes survive process restart. A successful response acknowledges its submitted snapshot. Keep newer local changes pending.
5. Use the remote revision for conditional updates. On conflict, keep both valid versions and follow the game's selection or merge rule. Only a confirmed missing slot enters the first-upload policy. If upload status is uncertain, read the remote revision and content before retrying. Keep unsynced data on failure and retry at an owned checkpoint or reconnect. Isolate callbacks and pending saves by provider, project environment, player and slot.

### Select a cloud save adapter

Reuse the game's neutral service facade when it already exists. The optional adapter exposes download and upload. Download returns bytes plus their opaque revision or an explicit missing, unavailable or failed result. Upload takes the committed bytes and the expected revision and returns the accepted revision or a conflict, unavailable or failed result. Keep provider identities, permissions, key mapping and format wrapping inside the adapter. The SO and its local writer use the same data and path when the provider changes. Moving existing cloud data to another provider has its own verified transfer and account mapping.

For Unity, use Player Files from the Unity adapter and map the remote revision to `WriteLock`. Associate downloaded bytes with their own revision when metadata and file content arrive separately. Use the normal configured provider and authentication session in a YoFaPlugin receiver. Other backends implement the same contract with their conditional write and file or document storage. A local-only game needs only its local writer. [Unity Player Files](https://docs.unity.com/en-us/cloud-save/tutorials/unity-sdk#player-files)

### Verify the receiving game

Use the normal startup and a gameplay checkpoint. Cold-start offline and check that the committed data reloads. Check that a failed local write submits nothing, failed upload keeps pending data, and a stale cloud revision preserves both copies. For a configured provider, upload committed bytes to a test-owned slot, then restore through the real game reader on a fresh run with the same account and compare the data and checksum. Keep local verification, adapter tests, and live provider readback attributable to their actual environment.
