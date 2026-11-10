using Godot;

// "This item can work blocks."
//
// Policy (kept in data, not engine code):
//   - equipped item WITH this component: mining/placing gated by the flags
//   - equipped item WITHOUT it: can neither mine nor place (that's a sword)
//   - empty hands: mine + place still allowed (default, like fists)
//
// PlacesBlockId closes the old TODO: WHICH block gets placed is item data
// now, resolved through Registries.Blocks ("ironbound:stone", "ironbound:grass",
// a mod's "camps:trap_floor", ...). The engine no longer names any block.
[GlobalClass]
public partial class BlockInteract : ItemComponent {

    [Export] public bool CanMine = true;
    [Export] public bool CanPlace = true;

    [Export] public string PlacesBlockId = "ironbound:stone";
}
