using Godot;
using MoonSharp.Interpreter;
using System;
using System.IO;

public partial class ModLoader : Node {

    public override void _Ready() {
        UserData.RegisterType<ItemDefinition>();
        LoadAllMods();
    }

    public void LoadAllMods() {
        string userModsPath = ProjectSettings.GlobalizePath("user://mods");

        if (!Directory.Exists(userModsPath)) {
            Directory.CreateDirectory(userModsPath);
            GD.Print($"[ModLoader] Created missing mods directory at: {userModsPath}");
            return;
        }

        string[] modFolders = Directory.GetDirectories(userModsPath);
        GD.Print($"[ModLoader] Scanning {modFolders.Length} mod folder(s)...");

        foreach (string modDir in modFolders) {
            string itemsFilePath = Path.Combine(modDir, "items.lua");

            if (File.Exists(itemsFilePath)) {
                ParseItemsFile(itemsFilePath, modDir);
            }
        }
    }

    private void ParseItemsFile(string filePath, string modDir) {
        MoonSharp.Interpreter.Script script = new MoonSharp.Interpreter.Script();

        try {
            DynValue result = script.DoFile(filePath);

            if (result.Type == DataType.Table) {
                Table itemsTable = result.Table;

                foreach (var pair in itemsTable.Pairs) {
                    if (pair.Key.Type == DataType.String && pair.Value.Type == DataType.Table) {
                        string itemId = pair.Key.String;
                        Table itemData = pair.Value.Table;

                        // Load icon texture if specified in Lua
                        Texture2D iconTexture = null;
                        string iconRelativePath = itemData.Get("icon").String;
                        if (!string.IsNullOrEmpty(iconRelativePath)) {
                            string absoluteIconPath = Path.Combine(modDir, iconRelativePath);
                            if (File.Exists(absoluteIconPath)) {
                                Image img = Image.LoadFromFile(absoluteIconPath);
                                if (img != null) {
                                    iconTexture = ImageTexture.CreateFromImage(img);
                                }
                            }
                        }

                        var definition = new ItemDefinition {
                            Id = itemId,
                            Name = itemData.Get("name").String ?? "Unnamed Item",
                            MaxStack = itemData.Get("max_stack").IsNotNil() ? (int)itemData.Get("max_stack").Number : 64,
                            Icon = iconTexture,
                            OnUseBehavior = itemData.Get("on_use").Function
                        };

                        // Extract optional custom stats
                        Table statsTable = itemData.Get("stats").Table;
                        if (statsTable != null) {
                            foreach (var statPair in statsTable.Pairs) {
                                if (statPair.Key.Type == DataType.String) {
                                    definition.CustomStats[statPair.Key.String] = statPair.Value;
                                }
                            }
                        }

                        ItemRegistry.Register(itemId, definition);
                    }
                }
            }
        }
        catch (Exception ex) {
            GD.PrintErr($"[ModLoader] Failed to load {filePath}: {ex.Message}");
        }
    }
}