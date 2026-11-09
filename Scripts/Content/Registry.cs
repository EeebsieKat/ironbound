using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// A registry (design doc: "Registries for items, components, slotsets, sockets,
// stances, actions and packs. Ids are namespaced as mod:name").
//
// The base game registers through the exact same calls a mod will - Origins
// is "just another mod" (OriginsContent.cs). Later, MoonSharp feeds these
// same registries from Lua; the API shape stays.
//
// Register = add (later loads replace earlier ones, logged - that's load
// order). Patch = mutate an existing entry in place via a callback, logged.
// Every mutation prints, so load-order debugging is greppable (doc: "every
// patch is logged for debugging").
public sealed class Registry<T> where T : class {

    private sealed class Entry {
        public T Value;
        public string Owner;
    }

    private readonly string name;
    private readonly Dictionary<string, Entry> entries = new();

    public Registry(string name) {
        this.name = name;
    }

    public T Register(string id, T value, string owner = "engine") {
        if (value == null) {
            GD.PushError($"[registry:{name}] tried to register null as '{id}'");
            return null;
        }

        if (!id.Contains(':')) {
            // Namespacing is what keeps mods from colliding - enforce it.
            GD.PushError($"[registry:{name}] id '{id}' is not namespaced (expected mod:name)");
        }

        if (entries.TryGetValue(id, out Entry existing)) {
            GD.PushWarning($"[registry:{name}] '{id}' already registered by '{existing.Owner}' - " +
                           $"'{owner}' replaces it (load order)");
        }

        entries[id] = new Entry { Value = value, Owner = owner };
        return value;
    }

    public T Get(string id) {
        return entries.TryGetValue(id, out Entry e) ? e.Value : null;
    }

    public bool TryGet(string id, out T value) {
        value = Get(id);
        return value != null;
    }

    public void Patch(string id, Action<T> mutator, string owner = "engine") {
        if (!entries.TryGetValue(id, out Entry e)) {
            GD.PushError($"[registry:{name}] can't patch '{id}' - not registered");
            return;
        }

        mutator(e.Value);
        GD.Print($"[registry:{name}] '{owner}' patched '{id}'");
    }

    public IEnumerable<string> Ids => entries.Keys;

    // All registered values (insertion order in practice - atlas layout
    // depends on it only cosmetically).
    public IEnumerable<T> Values => entries.Values.Select(e => e.Value);

    public void Dump() {
        GD.Print($"[registry:{name}] {entries.Count} entries:");
        foreach (KeyValuePair<string, Entry> kv in entries) {
            GD.Print($"  {kv.Key}  ({kv.Value.Owner})");
        }
    }
}

// The global registries. Grows over time: Stances, Actions, Packs, Sockets...
// Items first because equipment already consumes it (PlayerEquipment.LoadoutIds).
public static class Registries {

    public static readonly Registry<ItemDef> Items = new("items");

    public static readonly Registry<Block> Blocks = new("blocks");
}
