using Godot;

public partial class Hotbar : GridContainer {
    [Export] int hotbarSize = 9;
    [Export] Texture2D blankIcon;
    [Export] PackedScene slotScene;
    [Export] public Inventory mainInventory;

    private Item[] items;
    private Control[] slotNodes;

    public override void _Ready() {
        items = new Item[hotbarSize];
        slotNodes = new Control[hotbarSize];

        for (int i = 0; i < hotbarSize; i++) {
            Control slot = slotScene.Instantiate<Control>();
            AddChild(slot);
            slotNodes[i] = slot;
            UpdateSlotUI(i, blankIcon, "");

            int index = i;
            slot.GuiInput += (e) => OnSlotGuiInput(e, index);
        }
    }

    public Item GetItem(int index) => (index >= 0 && index < hotbarSize) ? items[index] : null;

    public void SetItem(int index, Item item) {
        if (index < 0 || index >= hotbarSize) return;
        items[index] = item;
        UpdateSlotView(index);
    }

    public bool AddItem(Item item) {
        if (item == null || item.Qty <= 0) return false;

        // 1. Try stacking with existing matching items
        for (int i = 0; i < hotbarSize; i++) {
            if (items[i] != null && items[i].ItemId == item.ItemId && items[i].Qty < items[i].MaxQty) {
                int spaceLeft = items[i].MaxQty - items[i].Qty;
                int amountToAdd = System.Math.Min(spaceLeft, item.Qty);
                items[i].Qty += amountToAdd;
                item.Qty -= amountToAdd;
                UpdateSlotView(i);
                if (item.Qty <= 0) return true;
            }
        }

        // 2. Try placing into empty slots
        for (int i = 0; i < hotbarSize; i++) {
            if (items[i] == null) {
                items[i] = item.DeepCopy();
                item.Qty = 0;
                UpdateSlotView(i);
                return true;
            }
        }

        return item.Qty == 0;
    }

    public void UpdateSlotView(int index) {
        Item item = items[index];
        if (item == null || item.Qty <= 0) {
            items[index] = null;
            UpdateSlotUI(index, blankIcon, "");
        } else {
            string text = item.MaxQty > 1 ? item.Qty.ToString() : "";
            UpdateSlotUI(index, item.Icon ?? blankIcon, text);
        }
    }

    private void UpdateSlotUI(int index, Texture2D icon, string text) {
        var texRect = slotNodes[index].GetNode<TextureRect>("TextureRect");
        var label = slotNodes[index].GetNode<Label>("Label");
        texRect.Texture = icon;
        label.Text = text;
    }

    private void OnSlotGuiInput(InputEvent @event, int index) {
        if (!(@event is InputEventMouseButton mouseEvent) || !mouseEvent.Pressed) return;

        Item cursorItem = mainInventory.GetCursorItem();

        // Shift-click to send to main inventory
        if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.ShiftPressed) {
            Item itemToMove = items[index];
            if (itemToMove == null) return;

            if (mainInventory != null && mainInventory.AddItem(itemToMove)) {
                SetItem(index, null);
            }
            return;
        }

        // Standard left click swap/pickup
        if (mouseEvent.ButtonIndex == MouseButton.Left) {
            if (cursorItem == null) {
                if (items[index] != null) {
                    mainInventory.SetCursorItem(items[index]);
                    SetItem(index, null);
                }
            } else {
                if (items[index] == null) {
                    SetItem(index, cursorItem);
                    mainInventory.SetCursorItem(null);
                } else if (items[index].ItemId == cursorItem.ItemId && items[index].Qty < items[index].MaxQty) {
                    int space = items[index].MaxQty - items[index].Qty;
                    int take = System.Math.Min(space, cursorItem.Qty);
                    items[index].Qty += take;
                    cursorItem.Qty -= take;
                    if (cursorItem.Qty <= 0) mainInventory.SetCursorItem(null);
                    UpdateSlotView(index);
                } else {
                    Item temp = items[index];
                    SetItem(index, cursorItem);
                    mainInventory.SetCursorItem(temp);
                }
            }
            mainInventory.RefreshCursorUI();
        }
    }
}