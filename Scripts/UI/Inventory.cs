using Godot;

public partial class Inventory : GridContainer {
    [Export] int inventorySize = 27;
    [Export] Texture2D blankIcon;
    [Export] PackedScene slotScene;
    [Export] TextureRect cursorItemUI;
    [Export] Hotbar hotbarNode;

    private Item[] items;
    private Control[] slotNodes;
    private Item cursorItem = null;
    private int hoveredSlotIndex = -1;

    public override void _Ready() {
        items = new Item[inventorySize];
        slotNodes = new Control[inventorySize];

        if (cursorItemUI != null) cursorItemUI.Visible = false;

        for (int i = 0; i < inventorySize; i++) {
            Control slot = slotScene.Instantiate<Control>();
            AddChild(slot);
            slotNodes[i] = slot;
            UpdateSlotUI(i, blankIcon, "");

            int index = i;
            slot.GuiInput += (e) => OnSlotGuiInput(e, index);
            slot.MouseEntered += () => hoveredSlotIndex = index;
            slot.MouseExited += () => { if (hoveredSlotIndex == index) hoveredSlotIndex = -1; };
        }
    }

    public override void _Process(double delta) {
        if (cursorItem != null && cursorItemUI != null) {
            cursorItemUI.GlobalPosition = GetGlobalMousePosition() - (cursorItemUI.Size / 2);
        }
    }

    public override void _Input(InputEvent @event) {
        // Hotbar selection swap via keys 1-9 when hovering inventory slots
        if (hoveredSlotIndex != -1 && @event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo) {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key9) {
                int hotbarIndex = (int)keyEvent.Keycode - (int)Key.Key1;
                if (hotbarNode != null) SwapWithHotbar(hoveredSlotIndex, hotbarIndex);
            }
        }
    }

    public Item GetCursorItem() => cursorItem;
    public void SetCursorItem(Item item) {
        cursorItem = (item != null && item.Qty > 0) ? item : null;
        RefreshCursorUI();
    }

    public bool AddItem(Item item) {
        if (item == null || item.Qty <= 0) return false;

        // 1. Stack into existing slots
        for (int i = 0; i < inventorySize; i++) {
            if (items[i] != null && items[i].ItemId == item.ItemId && items[i].Qty < items[i].MaxQty) {
                int space = items[i].MaxQty - items[i].Qty;
                int add = System.Math.Min(space, item.Qty);
                items[i].Qty += add;
                item.Qty -= add;
                UpdateSlotView(i);
                if (item.Qty <= 0) return true;
            }
        }

        // 2. Put into empty slots
        for (int i = 0; i < inventorySize; i++) {
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

    public void RefreshCursorUI() {
        if (cursorItemUI == null) return;
        if (cursorItem == null || cursorItem.Qty <= 0) {
            cursorItemUI.Visible = false;
            cursorItem = null;
            return;
        }

        cursorItemUI.Visible = true;
        cursorItemUI.Texture = cursorItem.Icon ?? blankIcon;
        var label = cursorItemUI.GetNode<Label>("Label");
        label.Text = cursorItem.MaxQty > 1 ? cursorItem.Qty.ToString() : "";
    }

    private void SwapWithHotbar(int invIndex, int hotbarIndex) {
        Item invItem = items[invIndex];
        Item hotbarItem = hotbarNode.GetItem(hotbarIndex);

        items[invIndex] = hotbarItem;
        hotbarNode.SetItem(hotbarIndex, invItem);
        UpdateSlotView(invIndex);
    }

    private void OnSlotGuiInput(InputEvent @event, int index) {
        if (!(@event is InputEventMouseButton mouseEvent) || !mouseEvent.Pressed) return;

        // Shift-click transfer to Hotbar
        if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.ShiftPressed) {
            Item itemToMove = items[index];
            if (itemToMove == null) return;

            if (hotbarNode != null && hotbarNode.AddItem(itemToMove)) {
                items[index] = null;
                UpdateSlotView(index);
            }
            return;
        }

        // Left Click: Pickup / Place / Merge Stack
        if (mouseEvent.ButtonIndex == MouseButton.Left) {
            if (cursorItem == null) {
                if (items[index] != null) {
                    SetCursorItem(items[index]);
                    items[index] = null;
                    UpdateSlotView(index);
                }
            } else {
                if (items[index] == null) {
                    items[index] = cursorItem;
                    SetCursorItem(null);
                    UpdateSlotView(index);
                } else if (items[index].ItemId == cursorItem.ItemId && items[index].Qty < items[index].MaxQty) {
                    int space = items[index].MaxQty - items[index].Qty;
                    int take = System.Math.Min(space, cursorItem.Qty);
                    items[index].Qty += take;
                    cursorItem.Qty -= take;
                    if (cursorItem.Qty <= 0) SetCursorItem(null);
                    UpdateSlotView(index);
                } else {
                    Item temp = items[index];
                    items[index] = cursorItem;
                    SetCursorItem(temp);
                    UpdateSlotView(index);
                }
            }
        }
        // Right Click: Pick up half / place single item
        else if (mouseEvent.ButtonIndex == MouseButton.Right) {
            if (cursorItem == null) {
                if (items[index] != null) {
                    int half = Mathf.CeilToInt(items[index].Qty / 2f);
                    cursorItem = new Item { ItemId = items[index].ItemId, Qty = half };
                    items[index].Qty -= half;
                    if (items[index].Qty <= 0) items[index] = null;
                    UpdateSlotView(index);
                    RefreshCursorUI();
                }
            } else {
                if (items[index] == null) {
                    items[index] = new Item { ItemId = cursorItem.ItemId, Qty = 1 };
                    cursorItem.Qty--;
                    if (cursorItem.Qty <= 0) SetCursorItem(null);
                    UpdateSlotView(index);
                } else if (items[index].ItemId == cursorItem.ItemId && items[index].Qty < items[index].MaxQty) {
                    items[index].Qty++;
                    cursorItem.Qty--;
                    if (cursorItem.Qty <= 0) SetCursorItem(null);
                    UpdateSlotView(index);
                }
            }
        }
    }
}