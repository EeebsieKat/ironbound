using Godot;
using MoonSharp.Interpreter;

// A component whose behavior lives in a Lua script (design doc: "Mods can
// register new component types with their own schema and hooks"). The engine
// side is pure mechanism: it remembers which registered component type this
// instance is, keeps the instance's data table, and forwards equip hooks to
// Lua. The schema is whatever the mod says it is.
public partial class ScriptedComponent : ItemComponent {

    [Export] public string ComponentId = "";

    // Instance data authored in the item def (Lua table).
    public Table Data;

    // The registered type's hooks; set when the item def is built.
    public CustomComponentSpec Spec;

    public override void OnEquip(PlayerEquipment equipment, ItemDef item) =>
        Spec?.InvokeEquip(this, item);

    public override void OnUnequip(PlayerEquipment equipment, ItemDef item) =>
        Spec?.InvokeUnequip(this, item);
}
