using Godot;
using System;
using System.Collections.Generic;

// The equipment authority: which ItemDef sits in which slot, and the event
// that everything else listens to. The player swaps its stance, the
// EquippedItemView swaps the rendered model, the MeleeHitbox swaps its damage
// numbers - all by subscribing to EquippedChanged. Nobody polls.
//
// Moddable form (design doc): the loadout can be a list of REGISTRY IDS
// ("ironbound:test_sword") instead of direct resource references - LoadoutIds
// below. Direct Loadout refs still work (that's what you set up already).
//
// Every equip goes through the event bus: "item.equipping" is cancellable
// (a Lua rule vetoes mid-swing swaps with it), "item.equipped" fires
// after. Equip() returns false when vetoed - Cycle() then stays put.
[GlobalClass]
public partial class PlayerEquipment : Node {

    [Export] public EquippedItemView View;
    [Export] public string SlotId = "main_hand";

    // Test harness: items to cycle through with the CycleAction key.
    // Position 0 is always empty hands.
    [Export] public Godot.Collections.Array<ItemDef> Loadout { get; set; } = new();

    // The moddable form: registry ids ("ironbound:test_sword"), resolved from
    // Registries.Items. Takes precedence over Loadout when non-empty.
    [Export] public string[] LoadoutIds { get; set; } = Array.Empty<string>();

    [Export] public string CycleAction = "cycle_item";

    // Equips on startup. Set this OR a loadout, not both.
    [Export] public ItemDef InitialItem { get; set; }

    public ItemDef Current { get; private set; }

    // C# event for engine-side systems; the moddable event bus (Events class)
    // wraps this later. Null = unequipped.
    public event Action<ItemDef> EquippedChanged;

    private readonly List<ItemDef> loadout = new();
    private bool loadoutResolved;
    private int loadoutIndex; // 0 = empty hands, 1..n = loadout[n-1]

    public override void _Ready() {
        // Deferred: mods load in the LuaEngine autoload (before the scene
        // tree exists), but deferred keeps this robust against ANY future
        // load-order change - deferred calls run after every _Ready in the
        // tree, so the registry is guaranteed populated by then.
        CallDeferred(nameof(ResolveLoadout));

        if (InitialItem != null) Equip(InitialItem);
    }

    public void ResolveLoadout() {
        if (loadoutResolved) return;
        loadoutResolved = true;
        loadout.Clear();

        if (LoadoutIds is { Length: > 0 }) {
            foreach (string id in LoadoutIds) {
                ItemDef def = Registries.Items.Get(id);
                if (def == null) {
                    GD.PushError($"[equipment] registry has no item '{id}' - check " +
                                 "LoadoutIds spelling (mods use 'mod:name' ids, e.g. " +
                                 "'ironbound:test_sword') and that the LuaEngine autoload " +
                                 "loaded the mods");
                    continue;
                }
                loadout.Add(def);
            }
        } else {
            foreach (ItemDef def in Loadout) {
                if (def != null) loadout.Add(def);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (CycleAction != "" && @event.IsActionPressed(CycleAction)) {
            Cycle();
        }
    }

    // Returns false when an "item.equipping" listener cancelled the swap.
    public bool Equip(ItemDef item) {
        if (item == Current) return true;

        Dictionary<string, object> data = new() {
            ["item"] = item?.Id ?? "",
            ["slot"] = SlotId,
        };
        if (Events.Emit("item.equipping", data).Cancelled) return false;

        Current?.RunOnUnequip(this);
        Current = item;

        if (View != null) {
            if (item != null) View.Equip(SlotId, item);
            else View.Clear();
        }

        Current?.RunOnEquip(this);
        EquippedChanged?.Invoke(item);
        Events.Emit("item.equipped", data);
        return true;
    }

    public void Unequip() => Equip(null);

    public void Cycle() {
        ResolveLoadout();

        int next = loadoutIndex >= loadout.Count ? 0 : loadoutIndex + 1;
        ItemDef nextItem = next == 0 ? null : loadout[next - 1];

        // Only commit the index if the swap wasn't vetoed.
        if (Equip(nextItem)) loadoutIndex = next;
    }

    // Shortcut: the equipped item's first component of type T, or null.
    public T GetComponent<T>() where T : ItemComponent => Current?.GetComponent<T>();

    // Find the equipment node from anywhere in the player's tree: walks up
    // the ancestors and searches each subtree. Hang the node wherever it's
    // convenient - the sword hitbox deep in the skeleton still finds it.
    public static PlayerEquipment Find(Node from) {
        for (Node n = from; n != null; n = n.GetParent()) {
            if (n is PlayerEquipment self) return self;
            PlayerEquipment found = FindDescendant(n);
            if (found != null) return found;
        }
        return null;
    }

    private static PlayerEquipment FindDescendant(Node node) {
        foreach (Node child in node.GetChildren()) {
            if (child is PlayerEquipment equipment) return equipment;
            PlayerEquipment found = FindDescendant(child);
            if (found != null) return found;
        }
        return null;
    }
}
