using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Loads mods from two homes:
//   res://mods   core mods that ship with the game (the ironbound mod
//                 lives here)
//   user://mods  drop-in folders - no editor, no project access. The folder
//                is created on first boot and its location printed.
// Core loads FIRST, user mods after - so a user mod patching or re-registering
// base content wins (doc: load order decides, later replaces earlier).
// The base game (Ironbound) loads through this exact path - there is no
// privileged content loader (design doc: "the base game uses the same API as
// third-party mods").
//
// Named LuaModLoader (not ModLoader) to stay out of the way of any existing
// ModLoader class in a project.
public static class LuaModLoader {

    // Bump when the Lua API surface changes incompatibly; mods declare the
    // version they target in mod.toml (api_version) and mismatched mods are
    // refused with a clear error instead of failing mysteriously.
    public const int EngineApiVersion = 1;

    public static void LoadAll() {
        EnsureUserModsFolder();

        List<Mod> core = Scan("res://mods");
        List<Mod> user = Scan("user://mods");

        // Manifests known across BOTH roots - user mods may depend on core
        // mods ("ironbound") and vice versa. Duplicate ids are an error, not a
        // crash: the first mod wins the dependency table, the duplicate still
        // loads and the registry logs the content replacement as usual.
        Dictionary<string, Mod> known = new();
        foreach (Mod mod in core.Concat(user)) {
            if (!known.TryAdd(mod.Id, mod)) {
                GD.PushError($"[mods] duplicate mod id '{mod.Id}' - '{known[mod.Id].Root}' " +
                             $"already claims it; dependencies resolve to the first, " +
                             "content still loads (the registry logs replacements)");
            }
        }

        foreach (Mod mod in Order(core).Concat(Order(user))) {
            LoadMod(mod, known);
        }

        int total = core.Count + user.Count;
        if (total == 0) {
            GD.PushWarning("[mods] no mods found (looked in res://mods and user://mods) " +
                           "- no content will exist");
        } else {
            GD.Print($"[mods] {core.Concat(user).Count(m => m.Loaded)}/{total} mods loaded " +
                     $"({core.Count} core, {user.Count} user)");
        }

        // Boot state, greppable: everything the engine will serve this
        // session. When something's missing, this dump says whether the mod
        // never loaded (empty) or registered different ids than expected.
        Registries.Items.Dump();
        Registries.Blocks.Dump();
    }

    private static void EnsureUserModsFolder() {
        if (!DirAccess.DirExistsAbsolute("user://mods")) {
            DirAccess.MakeDirRecursiveAbsolute("user://mods");
        }
        // Absolute location, so nobody has to guess where user:// points.
        // (Extracted on purpose: a string argument inside an interpolation
        // hole breaks older C# language versions - the local keeps this
        // compiling everywhere.)
        string location = ProjectSettings.GlobalizePath("user://mods");
        GD.Print($"[mods] user mods folder: {location}");
    }

    private static List<Mod> Scan(string root) {
        List<Mod> mods = new();

        string[] dirs = DirAccess.GetDirectoriesAt(root);
        if (dirs == null) return mods; // root itself doesn't exist - fine

        foreach (string dir in dirs.OrderBy(d => d)) {
            string modRoot = $"{root}/{dir}";
            if (!FileAccess.FileExists($"{modRoot}/mod.toml")) continue;

            Mod mod = ModManifest.Parse(modRoot, dir);
            if (mod.Id.Length == 0) {
                GD.PushError($"[mods] {modRoot}/mod.toml has no 'id' - skipped");
                continue;
            }
            mods.Add(mod);
        }

        return mods;
    }

    // Stable, dependency-aware ordering: repeatedly take the first mod whose
    // dependencies and load_after entries are all already ordered (or simply
    // absent from the folder). If the graph is cyclic, force the first
    // remaining mod with a warning rather than deadlocking the boot.
    private static List<Mod> Order(List<Mod> mods) {
        HashSet<string> allIds = mods.Select(m => m.Id).ToHashSet();
        List<Mod> ordered = new();
        List<Mod> remaining = new(mods);

        while (remaining.Count > 0) {
            Mod next = remaining.FirstOrDefault(m =>
                m.Dependencies.Concat(m.LoadAfter)
                    .All(d => !allIds.Contains(d) || ordered.Any(o => o.Id == d)));

            if (next == null) {
                next = remaining[0];
                GD.PushWarning($"[mods] load order cycle involving '{next.Id}' - forcing load");
            }

            ordered.Add(next);
            remaining.Remove(next);
        }

        return ordered;
    }

    private static void LoadMod(Mod mod, Dictionary<string, Mod> known) {
        if (mod.ApiVersion != EngineApiVersion) {
            GD.PushError($"[mods] '{mod.Id}' targets api_version {mod.ApiVersion}, " +
                         $"engine is {EngineApiVersion} - skipped");
            return;
        }

        // Missing dependency = hard, clearly-reported failure. Present but
        // failed to load (bad entry file, api mismatch) = warning: this mod
        // still runs, but expect errors about content that never registered.
        // (This loop once skipped ANY mod that had dependencies at all - the
        // condition was missing. The base mod has no deps, so nothing ever caught
        // it until cobalt declared one.)
        foreach (string dep in mod.Dependencies) {
            if (mod.Id == dep) continue;

            if (!known.ContainsKey(dep)) {
                GD.PushError($"[mods] '{mod.Id}' depends on '{dep}' which is not present - skipped");
                return;
            }

            if (!known[dep].Loaded) {
                GD.PushWarning($"[mods] '{mod.Id}' depends on '{dep}', which failed " +
                               "to load - continuing, expect missing content");
            }
        }

        if (mod.ClientEntry != null || mod.ServerEntry != null) {
            GD.Print($"[mods] '{mod.Id}' declares client/server entries; realm routing " +
                     "arrives with multiplayer - loading the shared entry only");
        }

        try {
            MoonSharpRuntime runtime = new(mod.Id, mod.Root);
            runtime.RunFile(mod.Entry);
            mod.Loaded = true;
            GD.Print($"[mods] loaded {mod.Id} {mod.Version}" +
                     (mod.Name.Length > 0 ? $" ({mod.Name})" : ""));
        } catch (Exception e) {
            GD.PushError($"[mods] '{mod.Id}' failed to load: {e.Message}");
        }
    }
}
