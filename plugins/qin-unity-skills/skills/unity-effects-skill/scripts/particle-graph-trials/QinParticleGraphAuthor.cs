using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.VFX;

// Install both files in an Editor-only folder in a URP receiver using VFX Graph 17.6.
public static class QinParticleGraphAuthor
{
    const string Folder = "Assets/GraphTrials";
    static readonly string[] Subjects = { "Rain", "Embers", "Fountain", "Leaves", "Motes" };

    // CLI entry for one graph. The five-brief replay explicitly calls CreateAll instead.
    public static void CreateSelected()
    {
        Create(Environment.GetEnvironmentVariable("QIN_VFX_SUBJECT"), SelectedTrial());
    }

    static int SelectedTrial()
    {
        string selection = Environment.GetEnvironmentVariable("QIN_VFX_TRIAL");
        if (string.IsNullOrEmpty(selection)) return 2;
        if (int.TryParse(selection, out int trial) && trial >= 1 && trial <= 3) return trial;
        throw new ArgumentException("QIN_VFX_TRIAL must be 1, 2 or 3.");
    }

    public static void CreateAll()
    {
        int trial = SelectedTrial();
        foreach (string subject in Subjects) CheckNewGraph($"{Folder}/{subject}-{trial}.vfx");
        foreach (string subject in Subjects) Create(subject, trial);
    }

    static void CheckNewGraph(string path)
    {
        if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
            throw new IOException("Graph destination is occupied: " + path);
    }

    // The three authored variations share the same physical brief. Each starts with an empty graph.
    public static void Create(string subject, int trial = 2)
    {
        if (!Subjects.Contains(subject) || trial < 1 || trial > 3)
            throw new ArgumentException("Choose Rain, Embers, Fountain, Leaves or Motes, and trial 1–3.");
        string path = $"{Folder}/{subject}-{trial}.vfx";
        CheckNewGraph(path);
        VFXManagerEditor.CheckVFXManager();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "GraphTrials");
        string mask = MaskPath(subject);
        bool newMask = !File.Exists(mask);
        if (newMask && File.Exists(mask + ".meta")) throw new IOException("Mask has an existing orphaned metadata file: " + mask);
        try
        {
            BuildGraph(path, subject, trial);
        }
        catch
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            if (newMask && File.Exists(mask)) AssetDatabase.DeleteAsset(mask);
            throw;
        }
    }

    static void BuildGraph(string path, string subject, int trial)
    {
        var resource = VisualEffectAssetEditorUtility.CreateNewAsset(path).GetResource();
        var graph = resource.GetGraph();
        var groups = new List<VFXUI.GroupInfo>();
        var spawn = Node<VFXBasicSpawner>(graph, 200, 0);
        spawn.label = "Continuous emission";
        var rate = Block<VFXSpawnerConstantRate>(spawn);
        graph.UIInfos.categories.Add(new VFXUI.CategoryInfo {name = "Local controls"});
        var driver = Expose(graph, "Driver", Vector3.zero, subject == "Motes"
            ? "Live attractor center in effect-local meters. XY preview supports ±1.25 m X, ±0.60 m Y."
            : "Emitter offset in effect-local meters, sampled at birth. XY preview supports ±1.25 m X, ±0.60 m Y.");
        float variation = trial - 2;
        var s = new SystemNodes(graph, spawn, groups, subject, 0, Capacity(subject));
        switch (subject)
        {
            case "Rain":
                rate.inputSlots[0].value = 62f + variation * 4f;
                Birth(s.init, driver, XY(-2.4f, 2.1f), XY(2.4f, 2.5f));
                Range(s.init, "velocity", XY(-.15f, -3.1f), XY(.2f, -2.65f));
                Set(s.init, "lifetime", 1.65f);
                GravityAt(s.update, .25f, -9.81f);
                var hit = Block<CollisionShape>(s.update);
                hit.SetSettingValue("shape", CollisionShapeBase.Type.Plane);
                hit.SetSettingValue("behavior", CollisionBase.Behavior.Kill);
                hit.SetSettingValue("collisionAttributes", CollisionBase.CollisionAttributesMode.WritePunctalContactOnly);
                hit.inputSlots[0].value = new UnityEditor.VFX.Plane(Vector3.up) {position = XY(0, -1.65f)};
                var ground = Expose(graph, "GroundHeight", -1.65f, "Live horizontal contact plane, in effect-local meters. Preview range −2.25 to −0.50 m.");
                Bind(ground, hit.inputSlots[0][0][1], new Vector2(-150, 1490), s.forces);
                var trigger = Block<TriggerEvent>(s.update);
                trigger.SetSettingValue("mode", TriggerEvent.Mode.OnCollide);
                trigger.inputSlots[0].value = 3u;
                s.Surface(new Color(.33f, .73f, 1), .21f, .3f, .07f, .93f, Orient.Mode.AlongVelocity);
                var evt = Node<VFXBasicGPUEvent>(graph, 1520, 0);
                evt.label = "Three droplets per contact";
                Wire(trigger.outputSlots[0], evt.inputSlots[0]);
                // 66 contacts/s × 3 fragments × 0.50 s = 99, with margin for coincident impacts.
                var splash = new SystemNodes(graph, evt, groups, "Splash", 1320, 128);
                var contact = Node<VFXAttributeParameter>(graph, 1160, 420);
                contact.SetSettingValue("attribute", "collisionEventPosition");
                contact.SetSettingValue("location", VFXAttributeLocation.Source);
                Wire(contact.outputSlots[0], Attribute(splash.init, "position").inputSlots[0]);
                splash.inputs.Add(new VFXNodeID(contact, 0));
                Range(splash.init, "velocity", XY(-2.7f, 1.5f), XY(2.7f, 3.5f + variation * .15f));
                Range(splash.init, "lifetime", .22f, .50f);
                GravityAt(splash.update, 0, -9.81f);
                splash.Surface(new Color(.67f, .90f, 1), .13f, .85f, .10f, .65f, Orient.Mode.AlongVelocity);
                splash.Finish();
                break;
            case "Embers":
                rate.inputSlots[0].value = 39f + variation * 2;
                Birth(s.init, driver, XY(-.42f, -1.70f), XY(.42f, -1.43f));
                Range(s.init, "velocity", XY(-1.3f, .45f), XY(1.35f, 1.65f));
                Range(s.init, "lifetime", 1.35f, 2.15f);
                GravityAt(s.update, .43f, 3.2f + variation * .21f);
                DragAt(s.update, .68f);
                s.Surface(new Color(1f, .39f, .068f), .16f, .72f, .14f, .52f, Orient.Mode.AlongVelocity);
                break;
            case "Fountain":
                rate.inputSlots[0].value = 76f + variation * 4;
                Birth(s.init, driver, XY(-.10f, -1.72f), XY(.10f, -1.65f));
                Range(s.init, "velocity", XY(-1.72f, 6.35f), XY(1.72f, 7.95f + variation * .14f));
                Range(s.init, "lifetime", 1.10f, 1.50f);
                GravityAt(s.update, 0, -9.81f);
                s.Surface(new Color(.30f, .86f, .98f), .17f, .66f, .08f, .78f, Orient.Mode.AlongVelocity);
                break;
            case "Leaves":
                rate.inputSlots[0].value = 14f + variation;
                Birth(s.init, driver, XY(-2.70f, .8f), XY(-1.30f, 2.15f));
                Range(s.init, "velocity", XY(.0f, -.3f), XY(1.35f, .5f));
                Range(s.init, "lifetime", 1.60f, 2.55f);
                Range(s.init, "angle", -65f, 65f, VariadicChannelOptions.Z);
                GravityAt(s.update, 3.3f + variation * .18f, -1.8f);
                DragAt(s.update, .62f);
                Curve(s.update, "angularVelocity", new AnimationCurve(new Keyframe(0, 45), new Keyframe(.32f, 200), new Keyframe(.72f, -75), new Keyframe(1, 50)), VariadicChannelOptions.Z);
                s.Surface(new Color(.97f, .88f, .53f), .37f, .85f, .12f, .77f, Orient.Mode.FaceCameraPlane);
                break;
            default:
                rate.inputSlots[0].value = 44f + variation * 2;
                var belt = Block<PositionShape>(s.init);
                belt.SetSettingValue("shape", PositionShapeBase.Type.Circle);
                var ring = TArcCircle.defaultValue;
                ring.circle.radius = 1.90f;
                belt.inputSlots[0].value = ring;
                Range(s.init, "velocity", XY(-.5f, -.25f), XY(.7f, .55f));
                Range(s.init, "lifetime", 1.25f, 2.05f);
                var pull = Block<ConformToSphere>(s.update);
                pull.inputSlots[0].value = new Sphere {radius = .44f + .035f * variation};
                Bind(driver, pull.inputSlots[0][0], new Vector2(-150, 1490), s.forces);
                pull.inputSlots[1].value = 2.05f;
                pull.inputSlots[2].value = 5.7f;
                pull.inputSlots[3].value = .20f;
                pull.inputSlots[4].value = 12f;
                s.Surface(new Color(.67f, .32f, 1f), .20f, 1f, .13f, .70f, Orient.Mode.FaceCameraPlane);
                break;
        }
        if (subject != "Motes") s.inputs.Add(new VFXNodeID(driver, driver.nodes.Single().id));
        s.Finish();
        graph.UIInfos.groupInfos = groups.ToArray();
        graph.SanitizeGraph();
        resource.WriteAssetWithSubAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    // Limits include the full flight, sprite radius, Driver offsets (±1.25, ±0.6, ±0.15 m)
    // and the rain ground range (-2.25 to -0.50 m). Expand on adapting these inputs.
    static AABox LocalBounds(string name)
    {
        Vector3 center = Vector3.zero;
        Vector3 size;
        switch (name)
        {
            case "Rain": size = XY(8.6f, 5.8f); center.y = .5f; break;
            case "Splash": size = XY(11f, 3.4f); center.y = -1.35f; break;
            case "Embers": size = XY(8.5f, 9.4f); center.y = 2.1f; break;
            case "Fountain": size = XY(8.3f, 7.0f); center.y = -.75f; break;
            case "Leaves": size = XY(13.6f, 7.9f); center = XY(2.50f, -.55f); break;
            default: throw new ArgumentException("Missing manual bounds for " + name);
        }
        size.z = .75f;
        return new AABox {center = center, size = size};
    }

    static uint Capacity(string subject)
    {
        switch (subject)
        {
            // At the lowest supported plane the slowest drop is airborne for at most 0.82 s.
            case "Rain": return 64;       // 66/s × 0.82 s = 55 falling drops plus frame overlap.
            case "Embers": return 96;     // 41/s × 2.15 s = 89.
            case "Fountain": return 128;  // 80/s × 1.50 s = 120.
            case "Leaves": return 44;     // 15/s × 2.55 s = 39.
            default: return 104;          // 46/s × 2.05 s = 95 motes.
        }
    }

    static Vector3 XY(float x, float y) => new Vector3(x, y, 0);

    static T Node<T>(VFXGraph graph, float x, float y) where T : VFXModel
    {
        var node = ScriptableObject.CreateInstance<T>();
        graph.AddChild(node);
        node.position = new Vector2(x, y);
        return node;
    }

    static T Block<T>(VFXContext context) where T : VFXBlock
    {
        var block = ScriptableObject.CreateInstance<T>();
        context.AddChild(block);
        return block;
    }

    static SetAttribute Attribute(VFXContext context, string name, VariadicChannelOptions channels = VariadicChannelOptions.XYZ)
    {
        var block = Block<SetAttribute>(context);
        block.SetSettingValue("attribute", name);
        block.SetSettingValue("channels", channels);
        return block;
    }

    static void Set(VFXContext context, string name, float value) => Attribute(context, name).inputSlots[0].value = value;

    static void Range(VFXContext context, string name, object low, object high, VariadicChannelOptions channels = VariadicChannelOptions.XYZ)
    {
        var block = Attribute(context, name, channels);
        block.SetSettingValue("Random", low is Vector3 ? RandomMode.PerComponent : RandomMode.Uniform);
        Assign(block.inputSlots[0], low);
        Assign(block.inputSlots[1], high);
    }

    static void Assign(VFXSlot slot, object value)
    {
        if (value is Vector3 v && slot.property.type == typeof(Position)) slot.value = (Position)v;
        else if (value is Vector3 w && slot.property.type == typeof(Vector)) slot.value = (Vector)w;
        else slot.value = value;
    }

    static void Wire(VFXSlot from, VFXSlot to)
    {
        if (!to.Link(from)) throw new InvalidOperationException($"Cannot connect {from.name} to {to.name}");
    }

    static VFXParameter Expose(VFXGraph graph, string name, object value, string description)
    {
        var parameter = ScriptableObject.CreateInstance<VFXParameter>();
        parameter.Init(value.GetType());
        parameter.SetSettingValue("m_ExposedName", name);
        parameter.SetSettingValue("m_Exposed", true);
        parameter.category = "Local controls";
        parameter.tooltip = description;
        parameter.value = value;
        graph.AddChild(parameter);
        return parameter;
    }

    static void Bind(VFXParameter param, VFXSlot input, Vector2 position, List<VFXNodeID> group = null)
    {
        var id = param.AddNode(position);
        Wire(param.outputSlots[0], input);
        param.GetNode(id).linkedSlots = new List<VFXParameter.NodeLinkedSlot> {
            new VFXParameter.NodeLinkedSlot {outputSlot = param.outputSlots[0], inputSlot = input}
        };
        group?.Add(new VFXNodeID(param, id));
    }

    static void Birth(VFXContext init, VFXParameter driver, Vector3 low, Vector3 high)
    {
        Range(init, "position", low, high);
        var offset = Attribute(init, "position");
        offset.SetSettingValue("Composition", AttributeCompositionMode.Add);
        Bind(driver, offset.inputSlots[0], new Vector2(-160, 450));
    }

    static void GravityAt(VFXContext update, float x, float y) => Block<Gravity>(update).inputSlots[0].value = (Vector)XY(x, y);
    static void DragAt(VFXContext update, float coefficient) => Block<Drag>(update).inputSlots[0].value = coefficient;

    static void Curve(VFXContext context, string attribute, AnimationCurve profile, VariadicChannelOptions channels = VariadicChannelOptions.XYZ)
    {
        var block = Block<AttributeFromCurve>(context);
        block.SetSettingValue("attribute", attribute);
        block.SetSettingValue("channels", channels);
        block.inputSlots[0].value = profile;
    }

    // Four shared 64 px alpha masks keep the output to one texture sample per particle.
    // Their source is mathematical, so the example has no external artwork dependency.
    static string ShapeName(string subject) =>
        subject == "Rain" || subject == "Splash" || subject == "Fountain" ? "Droplet" : subject;
    static string MaskPath(string subject) => $"{Folder}/{ShapeName(subject)}-mask.png";

    static Texture2D Silhouette(string name)
    {
        string shape = ShapeName(name);
        string path = MaskPath(name);
        const int side = 64;
        if (!File.Exists(path))
        {
            var pixels = new Color32[side * side];
            for (int y = 0; y < side; ++y)
                for (int x = 0; x < side; ++x)
                    pixels[y * side + x] = SampleShape(shape, (2 * x + 1f) / side - 1, (2 * y + 1f) / side - 1);
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Color SampleShape(string shape, float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        if (shape == "Leaves")
        {
            float along = (y + .86f) / 1.72f;
            float middle = .13f * Mathf.Sin(y * 3);
            float blade = .54f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Clamp01(along))), .85f);
            float edge = Mathf.Min(blade - Mathf.Abs(x - middle), .86f - Mathf.Abs(y));
            float vein = Mathf.Exp(-42 * Mathf.Abs(x - middle));
            float veins = Mathf.Pow(.5f + .5f * Mathf.Cos(25 * y - 21 * Mathf.Abs(x - middle)), 10);
            Color pigment = Color.Lerp(new Color(.39f, .56f, .12f), new Color(1f, .78f, .26f), .46f + .36f * y + .14f * veins);
            pigment = Color.Lerp(pigment, new Color(.93f, .92f, .51f), .65f * vein);
            pigment.a = Mathf.SmoothStep(0, 1, (edge + .026f) / .052f);
            return pigment;
        }
        if (shape == "Embers")
        {
            float uneven = r * (1 + .13f * Mathf.Cos(3 * Mathf.Atan2(y, x)));
            float core = Mathf.Exp(-7 * uneven * uneven);
            return new Color(1, .47f + .53f * core, .14f + .80f * core, Mathf.Exp(-4 * uneven * uneven) * Mathf.Clamp01((.96f - uneven) * 12));
        }
        if (shape == "Motes")
        {
            float core = Mathf.Exp(-24 * r * r);
            return new Color(.72f + .28f * core, .60f + .40f * core, 1, (.72f * core + .28f * Mathf.Exp(-4 * r * r)) * Mathf.Clamp01((1-r)*10));
        }
        float width = .51f + .22f * y;
        float drop = x * x / (width * width) + y * y / .79f;
        float highlight = Mathf.Exp(-13 * ((x + .13f) * (x + .13f) + (y - .19f) * (y - .19f)));
        return new Color(.65f + .35f * highlight, .85f + .15f * highlight, 1, Mathf.Clamp01((1 - drop) * 7) * (.65f + .35f * highlight));
    }

    sealed class SystemNodes
    {
        readonly VFXGraph graph;
        readonly List<VFXUI.GroupInfo> groups;
        readonly string name;
        readonly int x;
        readonly VFXContext spawn;
        public readonly VFXBasicInitialize init;
        public readonly VFXBasicUpdate update;
        readonly VFXComposedParticleOutput output;
        public readonly List<VFXNodeID> inputs = new List<VFXNodeID>();
        public readonly List<VFXNodeID> forces = new List<VFXNodeID>();
        readonly List<VFXNodeID> shading = new List<VFXNodeID>();

        public SystemNodes(VFXGraph graph, VFXContext spawn, List<VFXUI.GroupInfo> groups, string name, int x, uint capacity)
        {
            this.graph = graph;
            this.groups = groups;
            this.spawn = spawn;
            this.name = name;
            this.x = x;
            init = Node<VFXBasicInitialize>(graph, x + 200, 330);
            init.label = "Birth distribution and lifetime";
            update = Node<VFXBasicUpdate>(graph, x + 200, 1380);
            update.label = "Forces and events";
            output = Node<VFXComposedParticleOutput>(graph, x + 200, 2380);
            output.SetSettingValue("m_Topology", new ParticleTopologyPlanarPrimitive(VFXPrimitiveType.Quad));
            var shader = VFXShaderGraphHelpers.LoadShaderGraphAssetAtPath("Packages/com.unity.visualeffectgraph/ShaderGraph/VFXDefault.shadergraph");
            if (shader == null) throw new InvalidOperationException("VFX-compatible package Shader Graph failed to import.");
            output.SetSettingValue("shaderGraph", shader);
            output.ResyncDependencies();
            if (!output.inputSlots.Any(slot => slot.name == "_Color"))
                throw new InvalidOperationException($"Missing VFXDefault _Color slot. Shader {shader.name}, output {output.name}, slots [{string.Join(", ", output.inputSlots.Select(slot => slot.name))}]");
            output.label = "Particle silhouette and age response";
            spawn.LinkTo(init);
            init.LinkTo(update);
            update.LinkTo(output);
            init.GetData().title = name;
            init.SetSettingValue("capacity", capacity);
            if (name == "Motes")
            {
                // The target can reverse or teleport while an existing cohort is in flight.
                init.SetSettingValue("boundsMode", BoundsSettingMode.Automatic);
                init.inputSlots.Single(slot => slot.name == "boundsPadding").value = Vector3.one * .22f;
            }
            else
            {
                init.SetSettingValue("boundsMode", BoundsSettingMode.Manual);
                init.inputSlots[0].value = LocalBounds(name);
            }
        }

        public void Surface(Color tint, float size, float aspect, float onset, float tail, Orient.Mode orientation)
        {
            Block<Orient>(output).SetSettingValue("mode", orientation);
            Attribute(output, "scale", VariadicChannelOptions.X).inputSlots[0].value = aspect;
            Curve(output, "size", new AnimationCurve(new Keyframe(0, 0), new Keyframe(onset, size), new Keyframe(.55f, size * .88f), new Keyframe(1, size * .12f)));
            Curve(output, "alpha", new AnimationCurve(new Keyframe(0, 0), new Keyframe(onset, 1), new Keyframe(tail, .91f), new Keyframe(1, 0)));
            output.inputSlots.Single(slot => slot.name == "_BaseMap").value = Silhouette(name);
            var color = output.inputSlots.Single(slot => slot.name == "_Color");
            color.value = tint;
            var alpha = Node<VFXAttributeParameter>(graph, x - 145, 2450);
            alpha.SetSettingValue("attribute", "alpha");
            Wire(alpha.outputSlots[0], color[3]);
            shading.Add(new VFXNodeID(alpha, 0));
        }

        public void Finish()
        {
            Group(name + " — emission and birth", -75, 1320, new[]{spawn, init}, inputs);
            Group(name + " — simulation", 1300, 920, new VFXContext[]{update}, forces);
            Group(name + " — rendering", 2290, 800, new VFXContext[]{output}, shading);
        }

        void Group(string title, int y, int height, VFXContext[] contexts, List<VFXNodeID> extra = null)
        {
            var ids = contexts.Select(context => new VFXNodeID(context, 0)).ToList();
            if (extra != null) ids.AddRange(extra);
            groups.Add(new VFXUI.GroupInfo {title = title, position = new Rect(x - 220, y, 1090, height), contents = ids.ToArray()});
        }
    }
}
