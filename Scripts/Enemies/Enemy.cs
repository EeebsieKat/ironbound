using Godot;
using System;
using System.Collections.Generic;

// A basic enemy, wired into the engine's combat + event systems:
//   - IDamageable: the melee hitbox finds it and passes REAL numbers from the
//     equipped item's MeleeStats. (The old Hit() no-arg method fell into the
//     hitbox's fallback branch - it got called, but damage and knockback were
//     silently discarded.)
//   - entity.hurt (cancellable): every hit goes through the event bus first -
//     Lua rules can veto hits (i-frames, armor, immortality zones, boss phases).
//   - entity.died: after a killing blow - score, loot and mod rules hang here.
//
// EnemyId is the id Lua rules key off ("ironbound:grunt" by default) - set it
// per scene instance, e.g. "training:dummy" for an unkillable practice enemy.
public partial class Enemy : CharacterBody3D, IDamageable {

    public enum STATE { IDLE, ATTACK, STRUGGLE, DIE }
    // STRUGGLE is declared but not driven yet - yours to fill.

    [Export] public string EnemyId = "ironbound:grunt";
    [Export] public float SPEED = 4.0f;
    [Export] public float ATTACK_RANGE = 10.0f;
    [Export] public float GRAVITY = 9.8f * 2f;
    [Export] public float HEALTH = 100f;
    [Export] public float KnockbackDecay = 12f; // units per second

    // Kept from the original although this AI doesn't jump yet - removing it
    // would break scenes that already set it.
    [Export] public float JUMP_VEL = 2.0f;

    public STATE currentState;

    private LevelManager levelManager;
    public AnimationPlayer anim;
    public Node3D target;

    // Horizontal knockback impulse. Decays over time, so it STACKS with the
    // AI's own movement instead of being overwritten by it on the next frame.
    private Vector3 knockbackVelocity;

    public override void _Ready() {
        currentState = STATE.IDLE;

        // Both lookups fail soft - the enemy idles (harmlessly, no NRE) when
        // the scene is missing the player or the score keeper.
        levelManager = GetTree().Root.GetNodeOrNull<LevelManager>("LevelManager");
        anim = GetNodeOrNull<AnimationPlayer>("FemaleMortalModel/AnimationPlayer");
        if (anim == null) {
            GD.PushWarning($"[{EnemyId}] no AnimationPlayer at FemaleMortalModel/AnimationPlayer - check the scene");
        }
    }

    public override void _PhysicsProcess(double delta) {
        // Target: the player singleton first, "player" group as fallback.
        // Re-checked while null, so an enemy spawned before the player still
        // finds it.
        target ??= FemaleMortal.Instance as Node3D
                   ?? GetTree().GetFirstNodeInGroup("player") as Node3D;

        // Desired horizontal velocity for this frame, decided by the state.
        Vector3 move = Vector3.Zero;

        switch (currentState) {
            case STATE.IDLE:
                if (target != null && Position.DistanceTo(target.Position) < ATTACK_RANGE) {
                    currentState = STATE.ATTACK;
                }
                break;

            case STATE.ATTACK:
                if (target != null) {
                    Vector3 t = target.Position;
                    t.Y = Position.Y;
                    move = Position.DirectionTo(t) * SPEED;
                }
                break;

            case STATE.DIE:
                UpdateDie();
                break;
        }

        Vector3 velocity = Velocity;
        if (!IsOnFloor()) velocity.Y -= GRAVITY * (float)delta;

        // Knockback on top of the AI's movement, decaying.
        velocity.X = move.X + knockbackVelocity.X;
        velocity.Z = move.Z + knockbackVelocity.Z;
        knockbackVelocity = knockbackVelocity.MoveToward(Vector3.Zero, KnockbackDecay * (float)delta);

        // Face travel direction (movement, not AI intent, so hits turn too).
        Vector2 horizontal = new(velocity.X, velocity.Z);
        if (horizontal.LengthSquared() > 0.01f) {
            float facing = new Vector2(velocity.Z, velocity.X).Angle();
            Rotation = new Vector3(
                Rotation.X,
                (float)Mathf.LerpAngle(Rotation.Y, facing, delta * 10),
                Rotation.Z);
        }

        Velocity = velocity;
        MoveAndSlide();
        Animate(horizontal);
    }

    private void UpdateDie() {
        levelManager?.AddScore(1);
        QueueFree();
    }

    private void Animate(Vector2 horizontal) {
        if (anim == null) return;
        anim.Play(horizontal.LengthSquared() > 0.01f ? "walk" : "idle");
    }

    // -------------------------------------------------------------- damage --

    // IDamageable - the melee hitbox calls this with the equipped item's
    // MeleeStats numbers. Cancelling entity.hurt here = the hit never landed.
    public void Hit(int damage, Vector3 knockback) {
        Dictionary<string, object> data = new() {
            ["entity"] = EnemyId,
            ["damage"] = damage,
            ["health"] = HEALTH, // BEFORE the hit, so rules can spot killing blows
            ["item"] = FemaleMortal.Instance?.Equipment?.Current?.Id ?? "",
        };

        if (Events.Emit("entity.hurt", data).Cancelled) return; // rule vetoed it

        HEALTH -= damage;
        knockbackVelocity += new Vector3(knockback.X, 0, knockback.Z);
        GD.Print($"[{EnemyId}] health {HEALTH}");

        if (HEALTH <= 0) {
            Events.Emit("entity.died", new Dictionary<string, object> {
                ["entity"] = EnemyId,
                ["item"] = data["item"],
            });
            currentState = STATE.DIE;
        }
    }

    // Old no-arg form, kept so existing callers (and the Lua-friendly HasMethod
    // bridge) keep working: a plain hit for default damage, no knockback.
    public void Hit() => Hit(25, Vector3.Zero);
}
