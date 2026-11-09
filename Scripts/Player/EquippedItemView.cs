using Godot;

// Renders slotted items on body sockets (design doc: "items render where they
// are slotted - the slot decides the body socket, the item only fine-tunes
// the offset"). Add it anywhere under the player, register SocketBindings
// (e.g. "main_hand" -> SwordAttachment), then Equip() from inventory code.
// InitialItem lets you try it immediately from the inspector.
public partial class EquippedItemView : Node3D {

    [Export] public Godot.Collections.Array<SocketBinding> Sockets { get; set; } = new();
    [Export] public ItemDef InitialItem { get; set; }
    [Export] public string InitialSlot = "main_hand";

    private Node3D current;
    public string CurrentSlot { get; private set; }

    public override void _Ready() {
        if (InitialItem != null) Equip(InitialSlot, InitialItem);
    }

    public void Equip(string slotId, ItemDef item) {
        Clear();
        if (item == null || item.View == null) return;

        NodePath path = new NodePath();
        foreach (SocketBinding binding in Sockets) {
            if (binding != null && binding.SlotId == slotId && !binding.Socket.IsEmpty) {
                path = binding.Socket;
                break;
            }
        }

        if (path.IsEmpty) {
            GD.PushError($"EquippedItemView: no socket registered for slot '{slotId}'");
            return;
        }

        Node3D socket = GetNodeOrNull<Node3D>(path);
        if (socket == null) {
            GD.PushError($"EquippedItemView: socket node not found for slot '{slotId}' ({path})");
            return;
        }

        current = item.View.Instantiate() as Node3D;
        if (current == null) {
            GD.PushError($"EquippedItemView: view of '{item.Id}' does not have a Node3D root");
            return;
        }

        current.Position = item.SocketOffset;
        current.RotationDegrees = item.SocketRotationDegrees;
        socket.AddChild(current);
        CurrentSlot = slotId;
    }

    public void Clear() {
        if (current != null) current.QueueFree();
        current = null;
        CurrentSlot = null;
    }
}
