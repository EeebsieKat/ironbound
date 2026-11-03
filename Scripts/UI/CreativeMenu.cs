using Godot;

public partial class CreativeMenu : GridContainer {

    [Export] public Inventory targetInventory;
    [Export] public PackedScene creativeSlotScene; // Assign a button or slot prefab here

    public override void _Ready() {
        // Wait for ModLoader to finish loading items into ItemRegistry
        CallDeferred(nameof(PopulateMenu));
    }

    public void PopulateMenu() {
        // Clear old children if re-populating
        foreach (Node child in GetChildren()) {
            child.QueueFree();
        }

        foreach (string id in ItemRegistry.GetAllIds()) {
            ItemDefinition def = ItemRegistry.Get(id);
            if (def == null) continue;

            // Instantiate item button
            Button btn = new Button();
            btn.CustomMinimumSize = new Vector2I(55, 55);
            btn.Icon = def.Icon;
            btn.ExpandIcon = true;
            btn.TooltipText = def.Name;

            // Add click listener
            string itemId = id;
            btn.Pressed += () => {
                btn.ReleaseFocus(); // Prevents spacebar re-triggering
                if (targetInventory != null) {
                    targetInventory.AddItem(new Item { ItemId = itemId, Qty = 1 });
                }
            };

            AddChild(btn);
        }
    }
}