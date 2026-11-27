using UnityEngine;

// Compatibility for existing LocalFile subclasses, including their old save path and Play lifecycle.
// A separate SODataController can attach to an existing runtime SO and own its storage mode.
public abstract class SOLocalData : SODataController
{
    public override SODataMode Mode => SODataMode.LocalFile;
    protected override bool UsesLegacyPath => true;
    // code-gate: semantic-boundary=Legacy deserialization restores the subclass file name.
    public override void OnAfterDeserialize() => SetPathName();

    public override void OnEnable()
    {
        if (Application.isPlaying) LoadLocalData();
    }
}
