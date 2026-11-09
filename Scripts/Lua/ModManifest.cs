using Godot;
using System.Collections.Generic;

public sealed class Mod {

    public string DirectoryName;      // folder under res://mods
    public string Root;               // res://mods/<DirectoryName>

    // ---- mod.toml fields (design doc: id, version, engine API version,
    // dependencies, load_after, entry scripts per realm) ----
    public string Id = "";
    public string Name = "";
    public string Version = "";
    public int ApiVersion;
    public string Entry = "main.lua";
    public string ClientEntry;        // parsed now; realm routing arrives with multiplayer
    public string ServerEntry;
    public List<string> Dependencies = new();
    public List<string> LoadAfter = new();

    public bool Loaded;
}

// Minimal TOML-subset parser for mod.toml: comments (#), [sections],
// "quoted strings", integers, booleans, and string arrays. Deliberately not
// a general TOML parser - the manifest is engine-defined and small, so this
// is the whole grammar, documented by being 60 lines.
public static class ModManifest {

    public static Mod Parse(string root, string dirName) {
        Mod mod = new() { Root = root, DirectoryName = dirName };

        using FileAccess file = FileAccess.Open($"{root}/mod.toml", FileAccess.ModeFlags.Read);
        if (file == null) return mod;

        Dictionary<string, object> values = new();
        string section = "";

        int lineNo = 0;
        while (file.GetPosition() < file.GetLength()) {
            string line = file.GetLine().Trim();
            lineNo++;

            if (line.Length == 0 || line.StartsWith("#")) continue;

            if (line.StartsWith("[") && line.EndsWith("]")) {
                section = line[1..^1].Trim();
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0) {
                GD.PushWarning($"[mods] {root}/mod.toml:{lineNo}: not key = value, skipped");
                continue;
            }

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            string fullKey = section.Length == 0 ? key : $"{section}.{key}";

            values[fullKey] = ParseValue(value, root, lineNo);
        }

        mod.Id = AsString(values, "id");
        mod.Name = AsString(values, "name");
        mod.Version = AsString(values, "version");
        mod.ApiVersion = AsInt(values, "api_version", 0);
        mod.Entry = AsString(values, "entry", "main.lua");
        mod.ClientEntry = AsString(values, "client_entry");
        mod.ServerEntry = AsString(values, "server_entry");
        mod.Dependencies = AsList(values, "dependencies", "depends_on");
        mod.LoadAfter = AsList(values, "load_after");

        return mod;
    }

    private static object ParseValue(string value, string root, int lineNo) {
        if (value.StartsWith("[") && value.EndsWith("]")) {
            string inner = value[1..^1];
            List<string> items = new();
            foreach (string part in inner.Split(',')) {
                string item = part.Trim().Trim('"');
                if (item.Length > 0) items.Add(item);
            }
            return items;
        }

        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2) {
            return value[1..^1];
        }

        if (value == "true") return true;
        if (value == "false") return false;

        if (int.TryParse(value, out int number)) return number;

        GD.PushWarning($"[mods] {root}/mod.toml:{lineNo}: unrecognized value '{value}'");
        return value; // keep it as a string; unknown keys are ignored anyway
    }

    private static string AsString(Dictionary<string, object> values, string key, string fallback = "") {
        return values.TryGetValue(key, out object v) && v is string s ? s : fallback;
    }

    private static int AsInt(Dictionary<string, object> values, string key, int fallback) {
        return values.TryGetValue(key, out object v) && v is int i ? i : fallback;
    }

    private static List<string> AsList(Dictionary<string, object> values, params string[] keys) {
        foreach (string key in keys) {
            if (values.TryGetValue(key, out object v) && v is List<string> list) return list;
        }
        return new List<string>();
    }
}
