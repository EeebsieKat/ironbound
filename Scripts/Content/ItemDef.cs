using Godot;

// Content data for one item (design doc: items are registry entries, mods own
// content). Ids are namespaced mod:name - "ironbound" is the base game mod.
// This is the seed of the item registry; stack rules, hooks and the Lua
// component bridge come later per the doc.
[GlobalClass]
public partial class ItemDef : Resource {

    [Export] public string Id = "ironbound:test_sword";
    [Export] public string DisplayName = "Iron Sword";

    [Export] public PackedScene View;                    // mesh that renders at the socket

    // The slot decides the socket; the item only fine-tunes the transform.
    [Export] public Vector3 SocketOffset = Vector3.Zero;
    [Export] public Vector3 SocketRotationDegrees = Vector3.Zero;

    // Moveset: equipping this item swaps the player's animation set (design
    // doc: "a .glb containing animations is one moveset"). Null = keep the
    // player's default stance.
    [Export] public Stance Stance { get; set; }

    // The behavior bag (design doc: "components make items bags of
    // behavior"). Presence of a component = the item has that capability.
    [Export] public Godot.Collections.Array<ItemComponent> Components { get; set; } = new();

    // First component of type T on this item, or null if absent.
    public T GetComponent<T>() where T : ItemComponent {
        foreach (ItemComponent component in Components) {
            if (component is T typed) return typed;
        }
        return null;
    }

    public void RunOnEquip(PlayerEquipment equipment) {
        foreach (ItemComponent component in Components) {
            component?.OnEquip(equipment, this);
        }
    }

    public void RunOnUnequip(PlayerEquipment equipment) {
        foreach (ItemComponent component in Components) {
            component?.OnUnequip(equipment, this);
        }
    }
}
