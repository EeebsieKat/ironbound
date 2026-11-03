using Godot;

public class Item {
    public string ItemId;
    public int Qty;

    // Pulls data
    public ItemDefinition Definition => ItemRegistry.Get(ItemId);
    public string Name => Definition?.Name ?? "Unknown Item";
    public Texture2D Icon => Definition?.Icon;
    public int MaxQty => Definition?.MaxStack ?? 64;

    public Item DeepCopy() {
        return new Item { ItemId = ItemId, Qty = Qty };
    }
}