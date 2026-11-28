# DirectAccessOnline with Unity Cloud Save

## Connect the template

Copy the [one Unity Cloud Save channel](../assets/Providers/UnityCloudSave/UnityCloudSaveKeyStore.cs). Link the receiving Unity project to its UGS project and environment. Install `com.unity.services.cloudsave` (fixture version 3.4.2), reference `Unity.Services.CloudSave`, `Unity.Services.Authentication` and `Unity.Services.Core`, and define `SO_DATA_UNITY_CLOUD_SAVE` in the assembly containing the template and channel. Project credentials belong to the receiving game.

After initialization and authentication, open the shared runtime SO with `await data.OpenAsync(slot)`. The cloud mode selects the Unity adapter. To use a different service pass its `ISODataKeyStore` instance in that same call. Gameplay assigns `data.value = 5`. The mode sends only this field's value and prior revision. On slot exit await `data.CloseAsync()`.

## Verify the service boundary

The adapter scopes keys by slot and domain, validates the authenticated identity and uses the SDK write lock. Creation uses an account fence so two concurrent first writers cannot silently win. A lost acknowledgement or conflict requires reading the service again. The SDK fixture checks request contents and fresh reads against a simulated account. For a live receiver verify its real authenticated test account independently.
