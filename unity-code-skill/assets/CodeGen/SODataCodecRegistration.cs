using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SOData.CodeGen
{
    internal static class SODataCodecRegistration
    {
        private const string MethodName = "__SODataRegisterCodecs";

        internal static void Add(TypeDefinition domain, TypeReference scriptable, IMetadataScope runtime)
        {
            if (domain.Methods.Any(m => m.Name == MethodName))
                throw new InvalidOperationException(domain.FullName + " already declares the reserved method " + MethodName);
            ModuleDefinition module = domain.Module;
            MethodDefinition method = new MethodDefinition(MethodName,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, module.TypeSystem.Void);
            TypeReference preserve = new TypeReference("UnityEngine.Scripting", "PreserveAttribute", module,
                module.ImportReference(scriptable).Scope);
            method.CustomAttributes.Add(new CustomAttribute(new MethodReference(".ctor", module.TypeSystem.Void, preserve) { HasThis = true }));
            domain.Methods.Add(method);
            ILProcessor il = method.Body.GetILProcessor();
            MethodReference register = new MethodReference("Register", module.TypeSystem.Void,
                new TypeReference("", "SODataWire", module, runtime));
            register.GenericParameters.Add(new GenericParameter("T", register));
            HashSet<string> codecs = new HashSet<string>(StringComparer.Ordinal);

            // Keep the closed inherited layout reachable on AOT. For example, a concrete wallet
            // inheriting Balance<Vector3> emits Register<Vector3>() directly in its own assembly.
            for (TypeReference view = domain; view != null;)
            {
                if (view.FullName == "UnityEngine.ScriptableObject" || view.FullName == "SODataController" || view.FullName == "SOLocalData") break;
                TypeDefinition declaration = view.Resolve();
                foreach (FieldDefinition field in declaration.Fields)
                {
                    if (!SODataFieldWrites.IsDataField(field)) continue;
                    // A base registers its own closed private field types in its declaring context.
                    if (declaration != domain && !field.FieldType.ContainsGenericParameter) continue;
                    TypeReference type = SODataFieldWrites.ClosedFieldType(field.FieldType, view);
                    if (type.IsPointer || type.IsFunctionPointer || type.IsByReference) continue;
                    if (declaration != domain && !Accessible(type, domain)) continue;
                    if (!codecs.Add(type.Scope.Name + ":" + type.FullName)) continue;
                    GenericInstanceMethod call = new GenericInstanceMethod(register);
                    call.GenericArguments.Add(module.ImportReference(type, domain));
                    il.Append(il.Create(OpCodes.Call, call));
                }
                view = declaration.BaseType == null ? null : SODataFieldWrites.ClosedFieldType(declaration.BaseType, view);
            }
            il.Append(il.Create(OpCodes.Ret));
        }

        private static bool Accessible(TypeReference type, TypeDefinition domain)
        {
            if (type is GenericParameter) return true;
            if (type is ArrayType array) return Accessible(array.ElementType, domain);
            if (type is GenericInstanceType generic && !generic.GenericArguments.All(arg => Accessible(arg, domain))) return false;
            TypeDefinition definition = type.Resolve();
            if (definition == null) return false;
            if (!definition.IsNested) return definition.IsPublic || definition.Module.Assembly.Name.FullName == domain.Module.Assembly.Name.FullName;
            if (!definition.IsNestedPublic && definition.DeclaringType != domain) return false;
            return Accessible(definition.DeclaringType, domain);
        }
    }
}
