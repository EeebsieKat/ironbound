using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Loads every mod in res://mods. The base game (Origins) loads through this
// exact path - there is no privileged content loader (design doc: "the base
// game uses the same API as third-party mods").
//
// Named LuaModLoader (not ModLoader) to stay out of the way of any existing
// ModLoader class in a project.
public static class LuaModLoader {

    // Bump when the Lua API surface changes incompatibly; mods declare the
    // version they target in mod.toml (api_version) and mismatched mods are
    // refused with a clear error instead of failing mysteriously.
    public const int EngineApiVersion = 1;

    public static void LoadAll() {
        List<Mod> mods = Scan();
        if (mods.Count == 0) {
            GD.PushWarning("[mods] no mods found in res://mods - no content will exist");
        } else {
            foreach (Mod mod in Order(mods)) {
                LoadMod(mod);
            }

            GD.Print($"[mods] {mods.Count(m => m.Loaded)}/{mods.Count} mods loaded");
        }

        // Boot state, greppable: everything the engine will serve this
        // session. When something's missing, this dump says whether the mod
        // never loaded (empty) or registered different ids than expected.
        Registries.Items.Dump();
        Registries.Blocks.Dump();
    }

    private static List<Mod> Scan() {
        List<Mod> mods = new();

        string[] dirs = DirAccess.GetDirectoriesAt("res://mods");
        if (dirs == null) return mods;

        foreach (string dir in dirs.OrderBy(d => d)) {
            string root = $"res://mods/{dir}";
            if (!FileAccess.FileExists($"{root}/mod.toml")) continue;

            Mod mod = ModManifest.Parse(root, dir);
            if (mod.Id.Length == 0) {
                GD.PushError($"[mods] {root}/mod.toml has no 'id' - skipped");
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

    private static void LoadMod(Mod mod) {
        if (mod.ApiVersion != EngineApiVersion) {
            GD.PushError($"[mods] '{mod.Id}' targets api_version {mod.ApiVersion}, " +
                         $"engine is {EngineApiVersion} - skipped");
            return;
        }

        // Every manifest for the current folder set is known by now, so
        // missing dependencies are a hard, clearly-reported failure.
        foreach (string dep in mod.Dependencies) {
            if (mod.Id == dep) continue;
            GD.PushError($"[mods] '{mod.Id}' depends on '{dep}' which is not present - skipped");
            return;
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
