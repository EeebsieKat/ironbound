using Godot;

public partial class WorldItem : RigidBody3D {
    [Export] public string ItemId;
    [Export] public int Quantity = 1;

    private Sprite3D sprite;

    public override void _Ready() {
        sprite = GetNodeOrNull<Sprite3D>("Sprite3D");

        // Automatically style the world item using Lua-loaded data
        ItemDefinition def = ItemRegistry.Get(ItemId);
        if (def != null && sprite != null) {
            sprite.Texture = def.Icon;
        }

        // Connect pickup signal
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node body) {
        if (body.IsInGroup("Player")) {
            // Give item to player's inventory
            // Inventory inv = body.GetNode<Inventory>("UI/Inventory");
            // inv.AddInventoryItem(new Item { ItemId = ItemId, Qty = Quantity });
            
            QueueFree(); // Remove world drop object after pickup
        }
    }
}