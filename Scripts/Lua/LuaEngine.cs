using Godot;

// Autoload that boots the mod loader before the main scene exists, so
// registries are populated before ANY scene node code runs - BlockManager
// resolves terrain blocks in its _EnterTree, PlayerEquipment resolves its
// loadout after _Ready, mods can assume both.
//
// Setup: Project Settings > Globals > Autoload > add this script as
// "LuaEngine". In exported builds, include mods via the non-resource export
// filters: "*.lua, *.toml".
public partial class LuaEngine : Node {

    public override void _EnterTree() {
        // _EnterTree, NOT _Ready. This was a real bug: autoload _ready does
        // NOT run before the main scene - Godot queues ready notifications,
        // so the main scene's whole _enter_tree pass happens first. Loading
        // mods in _Ready meant BlockManager._EnterTree read an EMPTY registry
        // ("terrain blocks missing from the registry" + every chunk skipping
        // generation). Autoload _enter_tree, by contrast, is guaranteed to
        // run before the main scene is even instantiated.
        //
        // If other autoloads ever consume registry content, keep this one
        // listed ABOVE them in Project Settings > Globals > Autoload.
        LuaModLoader.LoadAll();
    }
}
