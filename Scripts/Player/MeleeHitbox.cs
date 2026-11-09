using Godot;
using System.Collections.Generic;

// Optional strongly-typed damage interface. Anything implementing it receives
// damage + knockback; anything else just needs a Hit() method - which is the
// same bridge the Lua runtime will use for modded entities later.
public interface IDamageable {
    void Hit(int damage, Vector3 knockback);
}

// Melee hitbox on the sword's Area3D (keep Monitoring ON, mask = enemies).
//
// All combat numbers now come from the EQUIPPED ITEM (design doc: items are
// data bags): MeleeStats supplies damage/knockback/hit window, the item's
// Stance supplies the attack animation name. No MeleeStats on the equipped
// item = swings deal no damage - so a pickaxe can swing but never hit.
//
// The old Damage/Knockback/HitWindow exports are gone; they live on the
// MeleeStats resource now, which is what makes per-weapon balance possible.
//
// Endgame per the design doc: hit windows live in action defs and keyframe
// method-call tracks (BeginSwing/EndSwing) instead of fractions.
public partial class MeleeHitbox : Area3D {

    [Export] public AnimationPlayer Anim { get; set; }
    // Fallback when the equipped item has no Stance; the stance usually wins.
    [Export] public StringName AttackAnim { get; set; } = "attack";

    private PlayerEquipment equipment;
    private MeleeStats melee;
    private StringName attackAnim = "attack";
    private bool inSwing;
    private readonly HashSet<CollisionObject3D> hitThisSwing = new();

    public override void _Ready() {
        equipment = PlayerEquipment.Find(this);
        if (equipment != null) {
            equipment.EquippedChanged += OnEquippedChanged;
        }
        Refresh();
    }

    private void OnEquippedChanged(ItemDef item) => Refresh();

    private void Refresh() {
        ItemDef item = equipment?.Current;
        melee = item?.GetComponent<MeleeStats>();

        StringName fromStance = item?.Stance?.AttackAnim;
        attackAnim = fromStance == null || fromStance == "" ? AttackAnim : fromStance;
    }

    public override void _PhysicsProcess(double delta) {
        if (Anim == null || melee == null) return; // nothing equipped can hit

        bool playingAttack = Anim.IsPlaying() && Anim.CurrentAnimation == attackAnim;
        if (playingAttack && !inSwing) {
            inSwing = true;          // swing (re)started: fresh target list
            hitThisSwing.Clear();
        } else if (!playingAttack) {
            inSwing = false;
        }

        if (!inSwing) return;

        double length = Anim.CurrentAnimationLength;
        if (length <= 0) return;
        float t = (float)(Anim.CurrentAnimationPosition / length);
        if (t < melee.HitWindowStart || t > melee.HitWindowEnd) return;

        // If enemies are Areas instead of bodies, check GetOverlappingAreas() here too.
        foreach (CollisionObject3D body in GetOverlappingBodies()) {
            if (!hitThisSwing.Add(body)) continue; // one hit per target per swing

            Vector3 dir = body.GlobalPosition - GlobalPosition;
            dir.Y = 0f;
            dir = dir.LengthSquared() > 0.001f ? dir.Normalized() : -GlobalTransform.Basis.Z;

            if (body is IDamageable damageable) {
                damageable.Hit(melee.Damage, dir * melee.Knockback);
            } else if (body.HasMethod("Hit")) {
                body.Call("Hit"); // Lua-friendly: modded entities only expose Hit()
            }
        }
    }
}
