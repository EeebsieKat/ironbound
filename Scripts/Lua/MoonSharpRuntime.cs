using Godot;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;

// 'Script' exists in BOTH Godot (GDScript resources) and MoonSharp - alias
// the MoonSharp one for this file. Godot's Script type is never used here.
using Script = MoonSharp.Interpreter.Script;

// The MoonSharp-backed Lua runtime: one isolated Script per mod. The doc's
// sandbox rules live here:
//   - CoreModules.Preset_SoftSandbox strips io / os / debug / package / load
//     / dofile from the Lua environment
//   - `require` is ours: it only resolves inside the mod's own folder (no
//     "../" escapes) plus the engine's 'api' module
//   - Lua never receives raw Godot nodes - only ids, strings, numbers and
//     tables (design doc: handles, never nodes)
//
// The API surface this exposes (all under the global `api` table):
//   api.log(msg)
//   api.items.register(id, def)      def = { display, view, socket_offset,
//                                      socket_rotation, stance, components }
//                                      view = 'res://....tscn' OR a table the
//                                      engine builds: { model | primitive,
//                                      size, radius, height, texture, color,
//                                      rotation, scale } - no editor needed
//   api.items.patch(id, patch)       same fields, applied in place
//   api.blocks.register(id, def)     def = { texture, top, bottom } - png
//                                      paths, the engine builds the Block
//                                      (or the old { resource = "....tres" })
//   api.events.on(name, fn, opts)    opts = { priority = 0 }
//   api.components.register(id, spec) spec = { on_equip, on_unequip }
//   api.player.attacking() / api.player.equipped()
//
// Paths anywhere in the api: 'res://...' and 'user://...' are absolute;
// anything else is RELATIVE to the mod's own folder ('textures/dirt.png'),
// which is what makes a mod a portable folder - no engine paths inside.
//
// Known gap vs the doc, stated plainly: per-hook instruction budgets.
// MoonSharp 2.x has no public instruction-counting API; a native runtime (or
// a coroutine-slicing scheme) is where that lands. Until then the Events
// strike system (3 errors -> handler auto-disabled) is the containment.
public sealed class MoonSharpRuntime : ILuaRuntime {

    public string ModId { get; }

    private readonly Script script;
    private readonly string modRoot; // res://mods/<folder>
    private readonly HashSet<string> loadedFiles = new(); // require cycle guard
    private readonly Dictionary<string, CustomComponentSpec> customComponents = new();

    public MoonSharpRuntime(string modId, string modRoot) {
        ModId = modId;
        this.modRoot = modRoot;

        // Soft sandbox: no io, os, debug, package, load, dofile.
        script = new Script(CoreModules.Preset_SoftSandbox);
        RegisterApi();
    }

    // ---------------------------------------------------------------- api --

    private void RegisterApi() {
        Table api = new(script);

        Table items = new(script);
        items.Set("register", DynValue.FromObject(script, (Action<string, Table>)RegisterItem));
        items.Set("patch", DynValue.FromObject(script, (Action<string, Table>)PatchItem));

        Table blocks = new(script);
        blocks.Set("register", DynValue.FromObject(script, (Action<string, Table>)RegisterBlock));

        Table events = new(script);
        events.Set("on", DynValue.FromObject(script, (Action<string, DynValue, DynValue>)OnEvent));

        Table components = new(script);
        components.Set("register", DynValue.FromObject(script, (Action<string, Table>)RegisterComponent));

        Table player = new(script);
        player.Set("attacking", DynValue.FromObject(script,
            (Func<DynValue>)(() => DynValue.NewBoolean(FemaleMortal.Instance?.IsAttacking ?? false))));
        player.Set("equipped", DynValue.FromObject(script, (Func<DynValue>)(() => {
            string id = FemaleMortal.Instance?.Equipment?.Current?.Id;
            return id == null ? DynValue.NewNil() : DynValue.NewString(id);
        })));

        api.Set("log", DynValue.FromObject(script, (Action<string>)Log));
        api.Set("items", DynValue.FromObject(script, items));
        api.Set("blocks", DynValue.FromObject(script, blocks));
        api.Set("events", DynValue.FromObject(script, events));
        api.Set("components", DynValue.FromObject(script, components));
        api.Set("player", DynValue.FromObject(script, player));

        script.Globals["api"] = api;
        // Our require shadows anything the preset left behind.
        script.Globals["require"] = DynValue.FromObject(script, (Func<string, DynValue>)Require);
    }

    private void Log(string message) => GD.Print($"[{ModId}] {message}");

    // ----------------------------------------------------------- require --

    public DynValue Require(string path) {
        if (path == "api") return script.Globals.Get("api");

        if (path.Contains("..")) {
            GD.PushError($"[{ModId}] require('{path}'): paths may not escape the mod folder");
            return DynValue.NewNil();
        }

        string file = path.EndsWith(".lua") ? path : path + ".lua";
        string full = $"{modRoot}/{file}";

        if (!loadedFiles.Add(full)) {
            GD.PushError($"[{ModId}] require('{path}'): circular require");
            return DynValue.NewNil();
        }

        string code = ReadText(full);
        if (code == null) {
            GD.PushError($"[{ModId}] require('{path}'): file not found ({full})");
            return DynValue.NewNil();
        }

        // MoonSharp 2.0 arg order: DoString(code, Table globalContext, string codeFriendlyName)
        return script.DoString(code, null, $"{ModId}/{file}");
    }

    public void RunFile(string relativePath) {
        string full = $"{modRoot}/{relativePath}";
        string code = ReadText(full);
        if (code == null) {
            GD.PushError($"[{ModId}] entry file not found: {full}");
            return;
        }
        script.DoString(code, null, $"{ModId}/{relativePath}");
    }

    private static string ReadText(string path) {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    // ------------------------------------------------------------ events --

    private void OnEvent(string name, DynValue handler, DynValue opts) {
        if (handler.Type != DataType.Function) {
            GD.PushError($"[{ModId}] events.on('{name}'): second argument must be a function");
            return;
        }

        int priority = 0;
        if (opts != null && opts.Type == DataType.Table) {
            DynValue p = opts.Table.Get("priority");
            if (p.Type == DataType.Number) priority = (int)p.Number;
        }

        Events.On(name, ctx => RaiseEvent(handler, ctx), priority, ModId);
    }

    public void RaiseEvent(object luaHandler, EventContext ctx) {
        if (luaHandler is not DynValue fn || fn.Type != DataType.Function) return;

        Table data = new(script);
        foreach (KeyValuePair<string, object> kv in ctx.Data) {
            data.Set(kv.Key, ToLua(kv.Value));
        }

        Table context = new(script);
        context.Set("data", DynValue.FromObject(script, data));
        context.Set("cancelled", DynValue.NewBoolean(ctx.Cancelled));
        context.Set("cancel", DynValue.FromObject(script, (Action)ctx.Cancel));

        // ScriptRuntimeException propagates to the Events strike system,
        // which logs it with this mod's id and disables the handler after 3.
        script.Call(fn, DynValue.FromObject(script, context));
    }

    // ------------------------------------------------------------ blocks --

    private void RegisterBlock(string id, Table def) {
        if (def == null) {
            GD.PushError($"[{ModId}] blocks.register('{id}'): definition table required");
            return;
        }

        // Old form, still supported: a Block resource you made yourself.
        string resource = GetString(def, "resource", null);
        if (resource != null) {
            Block block = ResourceLoader.Load<Block>(resource);
            if (block == null) {
                GD.PushError($"[{ModId}] blocks.register('{id}'): no Block resource at '{resource}'");
                return;
            }
            Registries.Blocks.Register(id, block, ModId);
            return;
        }

        // Pure-data form: png paths only, the engine builds the Block. This is
        // THE modding path - no .tres, no Godot editor.
        Texture2D sides = LoadTexture(GetString(def, "texture", null), id, "texture");
        Texture2D top = LoadTexture(GetString(def, "top", null), id, "top");
        Texture2D bottom = LoadTexture(GetString(def, "bottom", null), id, "bottom");

        if (sides == null && top == null && bottom == null && def.Keys.Any(k => k.Type == DataType.String)) {
            GD.PushWarning($"[{ModId}] blocks.register('{id}'): no usable textures (typo'd key?) - " +
                           "fine for air, invisible for anything else");
        }

        Registries.Blocks.Register(id, BuildBlock(sides, top, bottom), ModId);
    }

    // Every path a mod hands us goes through here:
    //   'res://...' / 'user://...'  absolute, used as-is
    //   anything else                RELATIVE to the mod's own folder - the
    //                                portable form ('textures/dirt.png')
    private string ResolvePath(string path) =>
        path.StartsWith("res://") || path.StartsWith("user://") ? path : $"{modRoot}/{path}";

    private Texture2D LoadTexture(string path, string id, string field) {
        if (path == null) return null;
        Texture2D texture = LoadTexture(ResolvePath(path));
        if (texture == null) {
            GD.PushError($"[{ModId}] blocks.register('{id}'): {field} '{path}' failed to load");
        }
        return texture;
    }

    // res:// textures go through ResourceLoader (imported); user:// textures
    // have no import step, so they load as raw images at runtime.
    private static Texture2D LoadTexture(string path) {
        if (path.StartsWith("user://")) {
            Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            return image != null ? ImageTexture.CreateFromImage(image) : null;
        }
        return ResourceLoader.Load<Texture2D>(path);
    }

    // Block.cs shape, known engine-side: three face textures, Textures is
    // DERIVED from them (read-only computed property). Direct assignment,
    // compile-checked - no reflection needed. If Block ever changes shape,
    // the compiler says so here instead of a runtime warning.
    private static Block BuildBlock(Texture2D sides, Texture2D top, Texture2D bottom) => new() {
        Texture = sides,
        TopTexture = top,
        BottomTexture = bottom,
    };

    // ------------------------------------------------------------- items --

    private void RegisterItem(string id, Table def) {
        if (def == null) {
            GD.PushError($"[{ModId}] items.register('{id}'): definition table required");
            return;
        }
        Registries.Items.Register(id, BuildItem(id, def), ModId);
    }

    private void PatchItem(string id, Table patch) {
        if (patch == null) return;
        Registries.Items.Patch(id, item => {
            if (Has(patch, "display")) item.DisplayName = GetString(patch, "display", item.DisplayName);
            if (Has(patch, "view")) item.View = BuildView(patch.Get("view"), id);
            if (Has(patch, "socket_offset")) item.SocketOffset = ToVector3(patch.Get("socket_offset").Table);
            if (Has(patch, "socket_rotation")) item.SocketRotationDegrees = ToVector3(patch.Get("socket_rotation").Table);
            if (Has(patch, "stance")) item.Stance = BuildStance(patch.Get("stance").Table);
            if (Has(patch, "components")) item.Components = BuildComponents(patch.Get("components").Table, id);
        }, ModId);
    }

    private ItemDef BuildItem(string id, Table def) {
        ItemDef item = new() {
            Id = id,
            DisplayName = GetString(def, "display", id),
            View = BuildView(def.Get("view"), id),
        };

        DynValue offset = def.Get("socket_offset");
        if (offset.Type == DataType.Table) item.SocketOffset = ToVector3(offset.Table);

        DynValue rotation = def.Get("socket_rotation");
        if (rotation.Type == DataType.Table) item.SocketRotationDegrees = ToVector3(rotation.Table);

        DynValue stance = def.Get("stance");
        if (stance.Type == DataType.Table) item.Stance = BuildStance(stance.Table);

        DynValue components = def.Get("components");
        if (components.Type == DataType.Table) item.Components = BuildComponents(components.Table, id);

        return item;
    }

    private static Stance BuildStance(Table t) => new() {
        IdleAnim = GetString(t, "idle", "idle"),
        WalkAnim = GetString(t, "walk", "walk"),
        JumpAnim = GetString(t, "jump", "jump"),
        AttackAnim = GetString(t, "attack", "attack"),
    };

    private PackedScene LoadView(string path, string itemId) {
        if (path == null) return null;
        PackedScene scene = ResourceLoader.Load<PackedScene>(ResolvePath(path));
        if (scene == null) {
            GD.PushWarning($"[{ModId}] item '{itemId}': no view scene at '{path}' - it will equip invisibly");
        }
        return scene;
    }

    // The no-editor view: builds the item's view scene from data. Two forms -
    //   view = 'res://....tscn'         your own scene (old form, still works)
    //   view = {                        built here, packed into a PackedScene:
    //     model     = 'res://....glb',  a model file (loads as a scene)
    //     -- or, with no model file, a primitive mesh:
    //     primitive = 'box'|'sphere'|'cylinder'|'capsule',
    //     size      = { x, y, z },     box only; default (0.3, 0.3, 1.0)
    //     radius    = .., height = .., the round primitives
    //     texture   = 'res://....png', optional albedo texture
    //     color     = '#rrggbb',       optional albedo color
    //     rotation  = { x, y, z },     degrees, on the view root
    //     scale     = 1.5 or { x, y, z },
    //   }
    // Socket placement stays the item's socket_offset / socket_rotation -
    // the slot decides the socket, the item only fine-tunes.
    private PackedScene BuildView(DynValue spec, string itemId) {
        if (spec == null || spec.IsNil()) return null;
        if (spec.Type == DataType.String) return LoadView(spec.String, itemId);
        if (spec.Type != DataType.Table) return null;
        Table view = spec.Table;

        Node3D root = new() { Name = "View" };

        string model = GetString(view, "model", null);
        if (model != null) {
            Node3D instance = LoadModelInstance(model, itemId);
            if (instance != null) {
                root.AddChild(instance);
                instance.Owner = root; // Pack() includes owned children
            }
            // null already logged - the item equips invisibly
        } else {
            MeshInstance3D mesh = new() { Name = "ViewMesh", Mesh = BuildPrimitiveMesh(view, itemId) };
            root.AddChild(mesh);
            mesh.Owner = root; // Pack() includes owned children
        }

        DynValue rotation = view.Get("rotation");
        if (rotation.Type == DataType.Table) root.RotationDegrees = ToVector3(rotation.Table);

        DynValue scale = view.Get("scale");
        if (scale.Type == DataType.Number) root.Scale = Vector3.One * (float)scale.Number;
        else if (scale.Type == DataType.Table) root.Scale = ToVector3(scale.Table);

        PackedScene packed = new();
        if (packed.Pack(root) != Error.Ok) {
            GD.PushError($"[{ModId}] item '{itemId}': failed to pack built view");
            return null;
        }
        return packed;
    }

    // Model loading, both homes:
    //   res://....glb   imported by the editor -> ResourceLoader (PackedScene)
    //   user://....glb  NO import step -> GltfDocument parses it at runtime
    // A model file whose root is not a 3D node logs and returns null.
    private Node3D LoadModelInstance(string path, string itemId) {
        path = ResolvePath(path);

        if (path.StartsWith("user://") &&
            (path.EndsWith(".glb") || path.EndsWith(".gltf"))) {
            GltfDocument document = new();
            GltfState state = new();
            if (document.AppendFromFile(ProjectSettings.GlobalizePath(path), state) != Error.Ok) {
                GD.PushError($"[{ModId}] item '{itemId}': can't parse model '{path}'");
                return null;
            }
            // GenerateScene returns Node, not Node3D - the root of a gltf
            // isn't guaranteed to be 3D. Pattern-match both casts the type
            // and rejects non-3D roots in one check.
            if (document.GenerateScene(state) is not Node3D modelScene) {
                GD.PushError($"[{ModId}] item '{itemId}': model root is not a 3D node");
                return null;
            }
            return modelScene;
        }

        PackedScene scene = ResourceLoader.Load<PackedScene>(path);
        if (scene == null) {
            GD.PushError($"[{ModId}] item '{itemId}': no model at '{path}' - it will equip invisibly");
            return null;
        }
        if (scene.Instantiate() is not Node3D instance) {
            GD.PushError($"[{ModId}] item '{itemId}': model root is not a 3D node");
            return null;
        }
        return instance;
    }

    private PrimitiveMesh BuildPrimitiveMesh(Table view, string itemId) {
        PrimitiveMesh mesh = GetString(view, "primitive", "box") switch {
            "box" => new BoxMesh {
                Size = Has(view, "size") ? ToVector3(view.Get("size").Table) : new Vector3(0.3f, 0.3f, 1.0f),
            },
            "sphere" => new SphereMesh {
                Radius = (float)GetNumber(view, "radius", 0.5),
                Height = (float)GetNumber(view, "height", 1),
            },
            "cylinder" => new CylinderMesh {
                TopRadius = (float)GetNumber(view, "radius", 0.25),
                BottomRadius = (float)GetNumber(view, "radius", 0.25),
                Height = (float)GetNumber(view, "height", 1),
            },
            "capsule" => new CapsuleMesh {
                Radius = (float)GetNumber(view, "radius", 0.25),
                Height = (float)GetNumber(view, "height", 1),
            },
            _ => FallbackBox(view, itemId),
        };

        StandardMaterial3D material = new();

        string texture = GetString(view, "texture", null);
        if (texture != null) {
            Texture2D loaded = ResourceLoader.Load<Texture2D>(texture);
            if (loaded == null) GD.PushError($"[{ModId}] item '{itemId}': view texture '{texture}' failed to load");
            else material.AlbedoTexture = loaded;
        }

        string color = GetString(view, "color", null);
        if (color != null) {
            if (Color.HtmlIsValid(color)) material.AlbedoColor = Color.FromHtml(color);
            else GD.PushError($"[{ModId}] item '{itemId}': '{color}' is not a valid #rrggbb color");
        }

        mesh.Material = material;
        return mesh;

        PrimitiveMesh FallbackBox(Table v, string id) {
            GD.PushError($"[{ModId}] item '{id}': unknown primitive " +
                         $"'{GetString(v, "primitive", "")}' (box/sphere/cylinder/capsule) - using box");
            return new BoxMesh { Size = new Vector3(0.3f, 0.3f, 1.0f) };
        }
    }

    private Godot.Collections.Array<ItemComponent> BuildComponents(Table components, string itemId) {
        Godot.Collections.Array<ItemComponent> built = new();

        foreach (DynValue key in components.Keys) {
            if (key.Type != DataType.String) continue;

            DynValue value = components.Get(key);
            if (value.Type != DataType.Table) continue;

            ItemComponent component = key.String switch {
                "melee" => BuildMelee(value.Table),
                "block_interact" => BuildBlockInteract(value.Table),
                _ => BuildScripted(key.String, value.Table),
            };

            if (component != null) built.Add(component);
            else GD.PushError($"[{ModId}] item '{itemId}': unknown component '{key.String}'");
        }

        return built;
    }

    private MeleeStats BuildMelee(Table t) => new() {
        Damage = (int)GetNumber(t, "damage", 1),
        Knockback = (float)GetNumber(t, "knockback", 6),
        HitWindowStart = (float)GetNumber(t, "hit_start", 0.25),
        HitWindowEnd = (float)GetNumber(t, "hit_end", 0.6),
    };

    private BlockInteract BuildBlockInteract(Table t) => new() {
        CanMine = GetBool(t, "can_mine", true),
        CanPlace = GetBool(t, "can_place", true),
        PlacesBlockId = GetString(t, "places", "ironbound:stone"),
    };

    // A component declared by the mod itself: mechanism in C#, behavior in Lua.
    private ScriptedComponent BuildScripted(string componentId, Table data) {
        if (!customComponents.TryGetValue(componentId, out CustomComponentSpec spec)) return null;
        return new ScriptedComponent { ComponentId = componentId, Spec = spec, Data = data };
    }

    private void RegisterComponent(string id, Table spec) {
        if (spec == null) {
            GD.PushError($"[{ModId}] components.register('{id}'): spec table required");
            return;
        }

        DynValue onEquip = spec.Get("on_equip");
        DynValue onUnequip = spec.Get("on_unequip");

        customComponents[id] = new CustomComponentSpec {
            Id = id,
            Runtime = this,
            OnEquip = onEquip.Type == DataType.Function ? onEquip : null,
            OnUnequip = onUnequip.Type == DataType.Function ? onUnequip : null,
        };
    }

    // ------------------------------------------------------------ helpers --

    public void RunHook(DynValue fn, params object[] args) {
        if (fn == null || fn.Type != DataType.Function) return;
        script.Call(fn, args.Select(ToLua).ToArray());
    }

    private DynValue ToLua(object value) {
        return value switch {
            null => DynValue.NewNil(),
            string s => DynValue.NewString(s),
            bool b => DynValue.NewBoolean(b),
            int i => DynValue.NewNumber(i),
            long l => DynValue.NewNumber(l),
            float f => DynValue.NewNumber(f),
            double d => DynValue.NewNumber(d),
            Vector3 v => VecTable(v.X, v.Y, v.Z),
            Vector3I v => VecTable(v.X, v.Y, v.Z),
            _ => DynValue.NewString(value.ToString()),
        };

        DynValue VecTable(double x, double y, double z) {
            Table t = new(script);
            t.Set("x", DynValue.NewNumber(x));
            t.Set("y", DynValue.NewNumber(y));
            t.Set("z", DynValue.NewNumber(z));
            return DynValue.FromObject(script, t);
        }
    }

    private static bool Has(Table t, string key) => t != null && !t.Get(key).IsNil();

    private static string GetString(Table t, string key, string fallback) {
        DynValue v = t?.Get(key);
        return v != null && v.Type == DataType.String ? v.String : fallback;
    }

    private static double GetNumber(Table t, string key, double fallback) {
        DynValue v = t?.Get(key);
        return v != null && v.Type == DataType.Number ? v.Number : fallback;
    }

    private static bool GetBool(Table t, string key, bool fallback) {
        DynValue v = t?.Get(key);
        return v != null && v.Type == DataType.Boolean ? v.Boolean : fallback;
    }

    private Vector3 ToVector3(Table t) => new(
        (float)GetNumber(t, "x", 0),
        (float)GetNumber(t, "y", 0),
        (float)GetNumber(t, "z", 0));
}

// A component TYPE registered from Lua: which hooks it has and which runtime
// owns them. ItemDefs hold ScriptedComponent instances pointing at one of
// these, carrying their own data table.
public sealed class CustomComponentSpec {

    public string Id;
    public MoonSharpRuntime Runtime;
    public DynValue OnEquip;
    public DynValue OnUnequip;

    public void InvokeEquip(ScriptedComponent instance, ItemDef item) =>
        Runtime?.RunHook(OnEquip, item.Id, instance.Data);

    public void InvokeUnequip(ScriptedComponent instance, ItemDef item) =>
        Runtime?.RunHook(OnUnequip, item.Id);
}
