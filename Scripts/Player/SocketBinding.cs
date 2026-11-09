using Godot;

// Maps a slot id ("main_hand") to the node that items in that slot render on.
// This is the slot -> socket mapping the design doc describes; slot layouts
// themselves become data when the slot runtime lands.
[GlobalClass]
public partial class SocketBinding : Resource {

    [Export] public string SlotId = "main_hand";
    [Export] public NodePath Socket;
}
