using System.Collections.Generic;
using Godot;

public static class ItemRegistry {
    private static Dictionary<string, ItemDefinition> registry = new Dictionary<string, ItemDefinition>();

    public static void Register(string id, ItemDefinition definition) {
        registry[id] = definition;
        GD.Print($"[ItemRegistry] Registered item: {id}");
    }

    public static ItemDefinition Get(string id) {
        if (registry.TryGetValue(id, out var definition)) {
            return definition;
        }
        GD.PrintErr($"[ItemRegistry] Warning: Attempted to get unregistered item ID: {id}");
        return null;
    }

    public static IEnumerable<string> GetAllItemIds() {
        return registry.Keys;
    }

    public static IEnumerable<string> GetAllIds() {
        return registry.Keys;
    }
}