using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class SODataInstrumentedAttribute : Attribute { }

// The compiler calls Assigned immediately after a managed store. Unattached SOs keep their normal behavior.
public static class SODataWriteHooks
{
    private static readonly ConditionalWeakTable<ScriptableObject, Action<string>> Writers =
        new ConditionalWeakTable<ScriptableObject, Action<string>>();
    private static int _bound;

    public static bool IsBound(ScriptableObject target) => Writers.TryGetValue(target, out _);

    public static void Bind(ScriptableObject target, Action<string> writer)
    {
        Type type = target.GetType();
        const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic;
        if (!Attribute.IsDefined(type.Assembly, typeof(SODataInstrumentedAttribute))
            || type.GetMethod("__SODataRegisterCodecs", flags, null, Type.EmptyTypes, null) == null)
            throw new InvalidOperationException("Install the SOData CodeGen assembly and recompile the domain before enabling storage.");
        if (Writers.TryGetValue(target, out _)) throw new InvalidOperationException("This runtime SO already has a storage owner.");
        Writers.Add(target, writer);
        System.Threading.Interlocked.Increment(ref _bound);
    }

    public static void Unbind(ScriptableObject target, Action<string> writer)
    {
        if (Writers.TryGetValue(target, out var current) && current == writer && Writers.Remove(target))
            System.Threading.Interlocked.Decrement(ref _bound);
    }

    public static void Assigned(ScriptableObject target, string fieldName)
    {
        if (System.Threading.Volatile.Read(ref _bound) > 0 && Writers.TryGetValue(target, out var write))
            write(fieldName);
    }
}
