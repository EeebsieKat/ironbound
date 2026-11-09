using Godot;

// "This item can hit things." MeleeHitbox reads these numbers live, every
// swing, from the currently equipped item.
//
// NO MeleeStats on an item = its swing deals no damage - a pickaxe can swing
// all day and never hurt anyone. Absence of a component is a statement.
[GlobalClass]
public partial class MeleeStats : ItemComponent {

    [Export] public int Damage = 1;
    [Export] public float Knockback = 6f;

    [Export(PropertyHint.Range, "0,1,0.01")] public float HitWindowStart = 0.25f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float HitWindowEnd = 0.6f;
}
