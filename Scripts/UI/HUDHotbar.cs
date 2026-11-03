using Godot;

public partial class HUDHotbar : GridContainer {
    [Export] int hotbarSize = 9;
    [Export] Texture2D blankIcon;
    [Export] PackedScene slotScene;
    [Export] Hotbar targetHotbar;

    private Control[] slotNodes;
    private int selectedIndex = 0;

    public override void _Ready() {
        slotNodes = new Control[hotbarSize];

        for (int i = 0; i < hotbarSize; i++) {
            Control slot = slotScene.Instantiate<Control>();
            AddChild(slot);
            slotNodes[i] = slot;
            UpdateSlotUI(i, blankIcon, "");
        }

        UpdateSelectionVisuals();
    }

    public override void _Process(double delta) {
        if (targetHotbar != null) {
            for (int i = 0; i < hotbarSize; i++) {
                Item item = targetHotbar.GetItem(i);
                if (item == null || item.Qty <= 0) {
                    UpdateSlotUI(i, blankIcon, "");
                } else {
                    string text = item.MaxQty > 1 ? item.Qty.ToString() : "";
                    UpdateSlotUI(i, item.Icon ?? blankIcon, text);
                }
            }
        }
    }

    public override void _Input(InputEvent @event) {
    if (Input.MouseMode == Input.MouseModeEnum.Visible) return;

    if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo) {
        if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key9) {
            int index = (int)keyEvent.Keycode - (int)Key.Key1;
            if (index < hotbarSize) {
                SetSelectedIndex(index);
            }
        }
    }

    if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed) {
        if (mouseEvent.ButtonIndex == MouseButton.WheelUp) {
            SetSelectedIndex((selectedIndex - 1 + hotbarSize) % hotbarSize);
        }
        else if (mouseEvent.ButtonIndex == MouseButton.WheelDown) {
            SetSelectedIndex((selectedIndex + 1) % hotbarSize);
        }
    }
}


    public int GetSelectedIndex() {
        return selectedIndex;
    }

    public void SetSelectedIndex(int index) {
        if (index < 0 || index >= hotbarSize) return;
        selectedIndex = index;
        UpdateSelectionVisuals();
    }

    public Item GetSelectedItem() {
        if (targetHotbar != null) {
            return targetHotbar.GetItem(selectedIndex);
        }
        return null;
    }


    private void UpdateSlotUI(int index, Texture2D icon, string text) {
        var texRect = slotNodes[index].GetNode<TextureRect>("TextureRect");
        var label = slotNodes[index].GetNode<Label>("Label");
        texRect.Texture = icon;
        label.Text = text;
    }

    private void UpdateSelectionVisuals() {
        for (int i = 0; i < hotbarSize; i++) {
            if (slotNodes[i] != null) {
                if (i == selectedIndex) {
                    slotNodes[i].Modulate = new Color(1.3f, 1.3f, 1.3f, 1f);
                } else {
                    slotNodes[i].Modulate = new Color(0.7f, 0.7f, 0.7f, 1f);
                }
            }
        }
    }
}