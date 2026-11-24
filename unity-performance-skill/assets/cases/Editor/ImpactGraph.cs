using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Qin.UnityPerformance.Cases
{
    // VFX Graph 17.6 exposes graph authoring through its editor assembly.
    // Compose its installed template in the disposable receiver so the pack ships no Unity graph bytes.
    public sealed partial class RenderingCases
    {
        private const string GraphPath = "Assets/PerformanceCases/Rendering/Impact.vfx";
        private const string Template = "Packages/com.unity.visualeffectgraph/Editor/Templates/Minimal_System.vfx";

        public static void Configure()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null) return;
            if (!AssetDatabase.CopyAsset(Template, GraphPath)) throw new InvalidOperationException("Could not copy the installed VFX Graph Minimal System template.");
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);

            Assembly editor = Assembly.Load("Unity.VisualEffectGraph.Editor");
            Type model = editor.GetType("UnityEditor.VFX.VFXModel", true);
            Type resourceMethods = editor.GetType("UnityEditor.VFX.VisualEffectResourceExtensions", true);
            Type objectMethods = editor.GetType("UnityEditor.VFX.VisualEffectObjectExtensions", true);
            object resource = objectMethods.GetMethod("GetResource").Invoke(null, new object[] { AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) });
            object graph = resourceMethods.GetMethod("GetGraph").Invoke(null, new[] { resource });
            IEnumerable children = (IEnumerable)model.GetProperty("children").GetValue(graph);
            object spawn = children.Cast<object>().Single(child => child.GetType().Name == "VFXBasicSpawner");
            object initialize = children.Cast<object>().Single(child => child.GetType().Name == "VFXBasicInitialize");
            spawn.GetType().GetMethod("UnlinkTo").Invoke(spawn, new object[] { initialize, 0, 0 });
            model.GetMethod("RemoveChild").Invoke(graph, new object[] { spawn, true });

            MethodInfo add = model.GetMethod("AddChild");
            MethodInfo set = model.GetMethod("SetSettingValue", new[] { typeof(string), typeof(object) });
            ScriptableObject impact = ScriptableObject.CreateInstance(editor.GetType("UnityEditor.VFX.VFXBasicEvent", true));
            set.Invoke(impact, new object[] { "eventName", "Impact" });
            add.Invoke(graph, new object[] { impact, -1, true });
            impact.GetType().GetMethod("LinkTo").Invoke(impact, new object[] { initialize, 0, 0 });

            Type attributeBlock = editor.GetType("UnityEditor.VFX.Block.SetAttribute", true);
            object source = Enum.Parse(attributeBlock.GetNestedType("ValueSource"), "Source");
            foreach (string attribute in new[] { "position", "color", "size", "lifetime" })
            {
                ScriptableObject block = ScriptableObject.CreateInstance(attributeBlock);
                set.Invoke(block, new object[] { "attribute", attribute });
                set.Invoke(block, new object[] { "Source", source });
                add.Invoke(initialize, new object[] { block, -1, true });
            }

            resourceMethods.GetMethod("WriteAssetWithSubAssets").Invoke(null, new[] { resource });
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
