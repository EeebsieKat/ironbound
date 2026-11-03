using Godot;

public partial class AddItem : Button {

    [Export] Inventory inv;
    [Export] string itemId;
    [Export] int qty = 1;

    public override void _Ready() {
        Pressed += AddNewItem;
    }

    private void AddNewItem() {
        ReleaseFocus();
        
        if (inv == null || string.IsNullOrEmpty(itemId)) {
            GD.PrintErr("[AddItem] Inventory reference or ItemId is missing!");
            return;
        }

        Item item = new Item {
            ItemId = itemId,
            Qty = qty
        };

        bool success = inv.AddItem(item);
        
        if (success) {
            GD.Print($"Added {item.Name} (x{qty}) to your inventory!");
        } else {
            GD.Print($"Inventory is full or could not add {itemId}.");
        }
    }
}