using System.Collections.Generic;
using Godot;
using MoonSharp.Interpreter;

public class ItemDefinition {
    public string Id { get; set; }
    public string Name { get; set; }
    public Texture2D Icon { get; set; }
    public int MaxStack { get; set; } = 64;
    
    // Custon Data
    public Dictionary<string, DynValue> CustomStats = new Dictionary<string, DynValue>();
    
    // Custom Lua function call
    public Closure OnUseBehavior { get; set; }
}