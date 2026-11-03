using Godot;

public partial class InventoryWindow : Control {
    [Export] private string toggleAction = "inventory";

    public override void _Ready() {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Input(InputEvent @event) {
        if (@event.IsActionPressed(toggleAction)) {
            Visible = !Visible;
            
            if (Visible) {
                // Inventory opened
                Input.MouseMode = Input.MouseModeEnum.Visible;
                MouseFilter = MouseFilterEnum.Stop;
            } else {
                // Inventory closed
                Input.MouseMode = Input.MouseModeEnum.Captured;
                MouseFilter = MouseFilterEnum.Ignore;
            }
        }
    }
}