using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace SOData.CodeGen
{
    public sealed class SODataPostProcessor : ILPostProcessor
    {
        private const string HooksAssembly = "SOData.Runtime";
        private const string MarkerName = "SODataInstrumentedAttribute";

        public override ILPostProcessor GetInstance() => new SODataPostProcessor();

        public override bool WillProcess(ICompiledAssembly input)
        {
            // A noEngineReferences gameplay assembly can still directly assign a public field on
            // a domain in another assembly. Its C# compiler needs only that domain assembly.
            return input.Name != HooksAssembly && !input.Name.EndsWith(".CodeGen", StringComparison.Ordinal);
        }

        public override ILPostProcessResult Process(ICompiledAssembly input)
        {
            try
            {
                using DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
                foreach (string directory in input.References.Select(Path.GetDirectoryName).Distinct())
                    if (!string.IsNullOrEmpty(directory)) resolver.AddSearchDirectory(directory);

                using MemoryStream pe = new MemoryStream(input.InMemoryAssembly.PeData);
                byte[] pdbData = input.InMemoryAssembly.PdbData;
                using MemoryStream symbols = pdbData == null || pdbData.Length == 0 ? null : new MemoryStream(pdbData);
                ReaderParameters reader = new ReaderParameters
                {
                    AssemblyResolver = resolver, ReadSymbols = symbols != null, SymbolStream = symbols,
                    SymbolReaderProvider = symbols == null ? null : new PortablePdbReaderProvider()
                };
                using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(pe, reader);
                if (assembly.CustomAttributes.Any(a => a.AttributeType.FullName == MarkerName
                    && a.AttributeType.Scope.Name == HooksAssembly)) return null;

                ModuleDefinition module = assembly.MainModule;
                TypeDefinition[] types = AllTypes(module.Types).ToArray();
                if (!IsParticipating(input, types)) return null;

                // All game assemblies may refer to the tiny leaf hook assembly, including a plain
                // SO or a caller whose C# source has no reason to import any persistence types.
                AssemblyNameReference runtime = module.AssemblyReferences.FirstOrDefault(a => a.Name == HooksAssembly);
                if (runtime == null)
                {
                    runtime = new AssemblyNameReference(HooksAssembly, new Version(0, 0, 0, 0));
                    module.AssemblyReferences.Add(runtime);
                }
                SODataFieldWrites writes = new SODataFieldWrites(module, runtime);
                bool changed = false;
                foreach (TypeDefinition type in types)
                {
                    foreach (MethodDefinition method in type.Methods)
                        if (writes.Instrument(method)) changed = true;
                    TypeReference scriptable = writes.DomainBase(type);
                    if (scriptable != null)
                    {
                        // Even a data-only class may be written entirely by other assemblies.
                        SODataCodecRegistration.Add(type, scriptable, runtime);
                        changed = true;
                    }
                }
                if (!changed) return null;

                TypeReference marker = new TypeReference("", MarkerName, module, runtime);
                MethodReference ctor = new MethodReference(".ctor", module.TypeSystem.Void, marker) { HasThis = true };
                assembly.CustomAttributes.Add(new CustomAttribute(ctor));
                using MemoryStream output = new MemoryStream();
                using MemoryStream outputSymbols = new MemoryStream();
                assembly.Write(output, new WriterParameters
                {
                    WriteSymbols = symbols != null, SymbolStream = symbols == null ? null : outputSymbols,
                    SymbolWriterProvider = symbols == null ? null : new PortablePdbWriterProvider()
                });
                return new ILPostProcessResult(new InMemoryAssembly(output.ToArray(), outputSymbols.ToArray()));
            }
            catch (Exception error)
            {
                return new ILPostProcessResult(null, new List<DiagnosticMessage>
                {
                    new DiagnosticMessage { DiagnosticType = DiagnosticType.Error,
                        MessageData = $"SO data instrumentation failed for {input.Name}. {error}" }
                });
            }
        }

        private static bool IsParticipating(ICompiledAssembly input, TypeDefinition[] types)
        {
            if (input.Name == "Assembly-CSharp" || input.Name.StartsWith("Assembly-CSharp-", StringComparison.Ordinal))
                return true;
            if (input.References.Any(path => Path.GetFileName(path) == HooksAssembly + ".dll")) return true;
            bool hasSource = false;
            foreach (TypeDefinition type in types)
            foreach (MethodDefinition method in type.Methods)
            foreach (SequencePoint point in method.DebugInformation.SequencePoints)
            {
                string path = point.Document.Url.Replace('\\', '/');
                if (path.IndexOf("/Library/PackageCache/", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)) { hasSource = true; continue; }
                if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    || path.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (!point.IsHidden) hasSource = true;
            }
            // A data-only class can have no sequence points. A player compiler can omit symbols.
            // In that case inspect non-engine source assemblies for real SO fields and writes.
            return !hasSource && !input.Name.StartsWith("Unity.", StringComparison.Ordinal)
                && !input.Name.StartsWith("UnityEngine.", StringComparison.Ordinal)
                && !input.Name.StartsWith("UnityEditor.", StringComparison.Ordinal);
        }

        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
        {
            foreach (TypeDefinition type in roots)
            {
                yield return type;
                foreach (TypeDefinition nested in AllTypes(type.NestedTypes)) yield return nested;
            }
        }
    }
}
