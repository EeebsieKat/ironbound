using Godot;

// Components make items bags of behavior (design doc). A component present on
// an ItemDef means "this item has that capability" - systems read the data,
// and the hooks below let a component act on equip/unequip. When the Lua
// runtime lands, scripted components map onto this same shape.
//
// To add a new capability: subclass this in C# (mechanism), then modders (and
// you) attach it to any ItemDef in the inspector (content). Engine code reads
// components; it never names a specific item.
[GlobalClass]
public abstract partial class ItemComponent : Resource {

    public virtual void OnEquip(PlayerEquipment equipment, ItemDef item) { }

    public virtual void OnUnequip(PlayerEquipment equipment, ItemDef item) { }
}
