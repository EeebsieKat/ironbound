using Godot;

// One stance = one moveset: the animation names a rig uses for locomotion and
// combat (design doc: "a .glb containing animations is one moveset"). Names
// live in data, so a mod can register its own moveset without touching engine
// code - the engine never names a specific animation.
//
// Roadmap per the doc: combo chains, cancel windows, per-action hit windows,
// and animation-event (method call) tracks driving hitboxes.
[GlobalClass]
public partial class Stance : Resource {

    [Export] public StringName IdleAnim = "idle";
    [Export] public StringName WalkAnim = "walk";
    [Export] public StringName JumpAnim = "jump";
    [Export] public StringName AttackAnim = "attack";
}
