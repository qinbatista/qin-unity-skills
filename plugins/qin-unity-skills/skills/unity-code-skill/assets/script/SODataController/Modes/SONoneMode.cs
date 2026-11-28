using System.Collections.Generic;

internal sealed class SONoneMode : SOStorageMode
{
    internal SONoneMode(SODataController owner) : base(owner) { }

    internal override void Assign<T>(ref T current, T value, string key)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        current = value;
        Owner.Changed(char.IsUpper(key[0]) ? char.ToLowerInvariant(key[0]) + key.Substring(1) : key);
    }
}
