using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace SOData.CodeGen
{
    internal sealed class SODataFieldWrites
    {
        private readonly ModuleDefinition module;
        private readonly IMetadataScope runtime;
        private readonly Dictionary<TypeDefinition, TypeReference> domains = new Dictionary<TypeDefinition, TypeReference>();
        private MethodReference assigned;

        internal SODataFieldWrites(ModuleDefinition module, IMetadataScope runtime)
        {
            this.module = module;
            this.runtime = runtime;
        }

        internal TypeReference DomainBase(TypeDefinition type)
        {
            if (type == null || type.IsValueType || type.IsInterface || type.FullName == "SODataController"
                || type.FullName == "SOLocalData" || type.BaseType == null) return null;
            if (domains.TryGetValue(type, out TypeReference scriptable)) return scriptable;
            for (TypeReference ancestor = type.BaseType; ancestor != null;)
            {
                if (ancestor.Namespace == "System"
                    || ancestor.Scope.Name.StartsWith("UnityEditor", StringComparison.Ordinal)
                    || ancestor.FullName == "UnityEngine.MonoBehaviour" || ancestor.FullName == "UnityEngine.Object") break;
                if (ancestor.FullName == "UnityEngine.ScriptableObject" && ancestor.Scope.Name == "UnityEngine.CoreModule")
                {
                    // The declaring domain carries the engine identity even if its caller has no engine refs.
                    scriptable = ancestor;
                    break;
                }
                TypeDefinition resolved = ancestor.Resolve();
                if (resolved == null) throw new InvalidOperationException("Unable to resolve SO ancestry: " + ancestor.FullName);
                ancestor = resolved.BaseType;
            }
            domains.Add(type, scriptable);
            return scriptable;
        }

        internal static bool IsDataField(FieldDefinition field)
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized) return false;
            return field.IsPublic || field.CustomAttributes.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeField"
                || a.AttributeType.FullName == "UnityEngine.SerializeReference");
        }

        internal bool Instrument(MethodDefinition method)
        {
            if (!method.HasBody || method.Name == "__SODataRegisterCodecs" || IsSerializationCallback(method)) return false;

            List<Instruction> stores = null;
            bool initialized = !method.IsConstructor || method.IsStatic;
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (!initialized && instruction.OpCode == OpCodes.Call && instruction.Operand is MethodReference call
                    && call.Name == ".ctor" && (call.DeclaringType.GetElementType().FullName == method.DeclaringType.FullName
                    || call.DeclaringType.GetElementType().FullName == method.DeclaringType.BaseType?.GetElementType().FullName))
                    initialized = true;
                if (instruction.OpCode != OpCodes.Stfld) continue;
                FieldReference field = (FieldReference)instruction.Operand;
                if (field.DeclaringType.IsValueType || field.DeclaringType.Namespace == "System") continue;
                TypeDefinition owner = field.DeclaringType.Resolve();
                if (owner == null) throw new InvalidOperationException("Unable to resolve field owner: " + field.FullName);
                TypeReference scriptable = DomainBase(owner);
                if (scriptable == null) continue;
                FieldDefinition definition = field.Resolve();
                if (definition == null) throw new InvalidOperationException("Unable to resolve field: " + field.FullName);
                // A field initializer can store into uninitialized 'this' before its base constructor.
                // Constructors of other objects are ordinary callers when they mutate a bound SO.
                if (!initialized && definition.DeclaringType == method.DeclaringType) continue;
                if (definition.DeclaringType.FullName == "SODataController" || definition.DeclaringType.FullName == "SOLocalData") continue;
                if (!IsDataField(definition)) continue;
                if (assigned == null)
                {
                    assigned = new MethodReference("Assigned", module.TypeSystem.Void,
                        new TypeReference("", "SODataWriteHooks", module, runtime));
                    assigned.Parameters.Add(new ParameterDefinition(module.ImportReference(scriptable)));
                    assigned.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
                }
                if (stores == null) stores = new List<Instruction>();
                stores.Add(instruction);
            }
            if (stores == null) return false;

            MethodBody body = method.Body;
            // New instructions can move a short branch out of range.
            body.SimplifyMacros();
            ILProcessor il = body.GetILProcessor();
            Dictionary<Instruction, Instruction> entries = new Dictionary<Instruction, Instruction>();
            Dictionary<FieldReference, VariableDefinition> locals = new Dictionary<FieldReference, VariableDefinition>();
            foreach (Instruction store in stores)
            {
                FieldReference field = (FieldReference)store.Operand;
                if (!locals.TryGetValue(field, out VariableDefinition value))
                {
                    value = new VariableDefinition(ClosedFieldType(field.FieldType, field.DeclaringType));
                    body.Variables.Add(value);
                    locals.Add(field, value);
                }

                // Input [..., target, value] -> [..., target, target, value]. Keep any volatile or
                // unaligned prefix attached to the original store. A throwing store never notifies.
                Instruction first = store;
                while (first.Previous != null && first.Previous.OpCode.OpCodeType == OpCodeType.Prefix)
                    first = first.Previous;
                Instruction spill = il.Create(OpCodes.Stloc, value);
                il.InsertBefore(first, spill);
                il.InsertBefore(first, il.Create(OpCodes.Dup));
                il.InsertBefore(first, il.Create(OpCodes.Ldloc, value));
                entries.Add(first, spill);
                Instruction name = il.Create(OpCodes.Ldstr, field.Name);
                il.InsertAfter(store, name);
                il.InsertAfter(name, il.Create(OpCodes.Call, assigned));
            }

            // Branches and protected-region boundaries pointed at the start of the original store.
            foreach (Instruction instruction in body.Instructions)
            {
                if (instruction.Operand is Instruction target && entries.TryGetValue(target, out Instruction entry))
                    instruction.Operand = entry;
                if (instruction.Operand is Instruction[] targets)
                    for (int i = 0; i < targets.Length; i++) targets[i] = Redirect(targets[i], entries);
            }
            foreach (ExceptionHandler handler in body.ExceptionHandlers)
            {
                handler.TryStart = Redirect(handler.TryStart, entries);
                handler.TryEnd = Redirect(handler.TryEnd, entries);
                handler.HandlerStart = Redirect(handler.HandlerStart, entries);
                handler.HandlerEnd = Redirect(handler.HandlerEnd, entries);
                handler.FilterStart = Redirect(handler.FilterStart, entries);
            }
            body.OptimizeMacros();
            return true;
        }

        private static bool IsSerializationCallback(MethodDefinition method)
        {
            if (method.HasParameters || method.ReturnType.MetadataType != MetadataType.Void) return false;
            if (method.Name != "OnBeforeSerialize" && method.Name != "OnAfterDeserialize"
                && !method.Name.EndsWith(".OnBeforeSerialize", StringComparison.Ordinal)
                && !method.Name.EndsWith(".OnAfterDeserialize", StringComparison.Ordinal)) return false;
            for (TypeDefinition type = method.DeclaringType; type != null; type = type.BaseType?.Resolve())
                if (type.Interfaces.Any(i => i.InterfaceType.FullName == "UnityEngine.ISerializationCallbackReceiver")) return true;
            return false;
        }

        private static Instruction Redirect(Instruction original, Dictionary<Instruction, Instruction> entries) =>
            original != null && entries.TryGetValue(original, out Instruction replacement) ? replacement : original;

        internal static TypeReference ClosedFieldType(TypeReference type, TypeReference receiver)
        {
            if (type is RequiredModifierType required) return ClosedFieldType(required.ElementType, receiver);
            if (type is OptionalModifierType optional) return ClosedFieldType(optional.ElementType, receiver);
            if (type is GenericParameter parameter && parameter.Type == GenericParameterType.Type
                && receiver is GenericInstanceType owner) return owner.GenericArguments[parameter.Position];
            if (type is ArrayType array) return new ArrayType(ClosedFieldType(array.ElementType, receiver), array.Rank);
            if (!(type is GenericInstanceType generic)) return type;
            GenericInstanceType closed = new GenericInstanceType(generic.ElementType);
            foreach (TypeReference argument in generic.GenericArguments)
                closed.GenericArguments.Add(ClosedFieldType(argument, receiver));
            return closed;
        }
    }
}
