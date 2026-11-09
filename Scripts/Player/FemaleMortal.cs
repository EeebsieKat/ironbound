using Godot;
using System.Collections.Generic;

// Player body: locomotion, jumping, facing, block interaction, attack state.
// Restructured toward the "thin body" idea in the Ironbound design doc:
//  - locomotion runs in _PhysicsProcess (was _Process)
//  - attack state is a plain bool driven by AnimationFinished (was async/await)
//  - animation names come from a Stance resource instead of hardcoded strings
//  - melee hit detection lives in MeleeHitbox on the sword's Area3D,
//    so the player no longer uses swordArea.Monitoring as a state flag
//  - the equipped item (via PlayerEquipment) drives the active stance and
//    gates mining/placing through its components - the body stays thin
public partial class FemaleMortal : CharacterBody3D {

    [ExportGroup("Movement")]
    [Export] public float Speed = 5f;
    [Export] public float JumpVelocity = 7f;
    [Export] public float Gravity = 9.8f * 2f;
    [Export] public float SwingDeceleration = 8f;
    [Export] public float TurnSpeed = 10f;

    [ExportGroup("Jump Feel")]
    [Export] public float CoyoteTime = 0.12f;
    [Export] public float JumpBufferTime = 0.12f;

    [ExportGroup("Model")]
    // Keep in sync with GameCam.ModelFacesPositiveZ. The original rotation math
    // (Vector2(vel.Z, vel.X).Angle() == atan2(vel.X, vel.Z)) assumes the model
    // faces +Z at Rotation.Y == 0 - hence the default true here.
    [Export] public bool ModelFacesPositiveZ = true;

    [ExportGroup("References")]
    [Export] public RayCast3D RayCast { get; set; }
    [Export] public MeshInstance3D BlockHighlight { get; set; }
    // Fallback moveset when nothing (or an item without a Stance) is equipped.
    [Export] public Stance DefaultStance { get; set; }
    // Optional: auto-found in _Ready if left empty.
    [Export] public PlayerEquipment Equipment { get; set; }

    // TODO(design doc): remove once the intent pipeline replaces direct input reads.
    public static FemaleMortal Instance { get; private set; }

    public AnimationPlayer anim;

    private bool attacking;
    private float attackTimeout;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private Stance fallbackStance;
    private Stance activeStance;

    public bool IsAttacking => attacking;

    public override void _Ready() {
        Instance = this;
        anim = GetNode<AnimationPlayer>("FemaleMortalModel/AnimationPlayer");
        anim.AnimationFinished += OnAnimationFinished;

        fallbackStance = DefaultStance ?? new Stance();
        activeStance = fallbackStance;

        // Equipping an item can swap the whole moveset (ItemDef.Stance).
        Equipment ??= PlayerEquipment.Find(this);
        if (Equipment != null) {
            Equipment.EquippedChanged += OnEquippedItemChanged;
            OnEquippedItemChanged(Equipment.Current); // apply startup equip
        }
    }

    private void OnEquippedItemChanged(ItemDef item) {
        activeStance = item != null && item.Stance != null ? item.Stance : fallbackStance;
    }

    public override void _PhysicsProcess(double delta) {
        float dt = (float)delta;
        Vector3 velocity = Velocity;

        // Full control normally; a swing bleeds off momentum instead.
        if (attacking) {
            float t = 1f - Mathf.Exp(-SwingDeceleration * dt);
            velocity.X = Mathf.Lerp(velocity.X, 0f, t);
            velocity.Z = Mathf.Lerp(velocity.Z, 0f, t);
        } else {
            velocity = ApplyMoveInput(velocity);
        }

        // Gravity + coyote time (grace period after walking off a ledge).
        if (IsOnFloor()) {
            coyoteTimer = CoyoteTime;
        } else {
            coyoteTimer -= dt;
            velocity.Y -= Gravity * dt;
        }

        // Jump buffering: a press slightly before landing still fires.
        if (Input.IsActionJustPressed("jump")) {
            jumpBufferTimer = JumpBufferTime;
        } else {
            jumpBufferTimer -= dt;
        }

        if (!attacking && jumpBufferTimer > 0f && coyoteTimer > 0f) {
            velocity.Y = JumpVelocity;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        Velocity = velocity;
        UpdateFacing(dt);
        HandleBlockInteraction();
        TickAttackTimeout(dt);

        MoveAndSlide();
        UpdateAnimation();
    }

    public override void _UnhandledInput(InputEvent @event) {
        // Separate action from "destroy": the old code swung the sword AND mined
        // the block behind it on the same button press.
        if (@event.IsActionPressed("attack")) {
            // "item.use" is the classic moddable hook (design doc) - cancellable,
            // so a listener (in Lua now) can veto it (cooldowns, curses, ...).
            Dictionary<string, object> data = new() { ["item"] = Equipment?.Current?.Id ?? "" };
            if (!Events.Emit("item.use", data).Cancelled) {
                TryStartAttack();
            }
        }
    }

    private Vector3 ApplyMoveInput(Vector3 velocity) {
        Vector2 input = Input.GetVector("left", "right", "up", "down");
        Camera3D camera = GetViewport().GetCamera3D();
        if (camera == null) return velocity;

        Vector3 camForward = -camera.GlobalTransform.Basis.Z;
        Vector3 camRight = camera.GlobalTransform.Basis.X;
        camForward.Y = 0f;
        camRight.Y = 0f;
        camForward = camForward.Normalized();
        camRight = camRight.Normalized();

        Vector3 direction = (camForward * -input.Y) + (camRight * input.X);
        return new Vector3(direction.X * Speed, velocity.Y, direction.Z * Speed);
    }

    private void UpdateFacing(float dt) {
        if (attacking) return;

        Vector2 horiz = new(Velocity.X, Velocity.Z);
        if (horiz.LengthSquared() < 0.01f) return;

        // horiz.Y is the Z component - same formula as the old Vector2(Z, X).Angle().
        float targetYaw = ModelFacesPositiveZ
            ? Mathf.Atan2(horiz.X, horiz.Y)
            : Mathf.Atan2(-horiz.X, -horiz.Y);

        float yaw = Mathf.LerpAngle(Rotation.Y, targetYaw, 1f - Mathf.Exp(-TurnSpeed * dt));
        Rotation = new Vector3(Rotation.X, yaw, Rotation.Z);
    }

    private void HandleBlockInteraction() {
        // The export works when the ray lives in the same scene. If the ray sits
        // on the camera (a separate scene instance), auto-find it under the
        // active camera instead.
        RayCast ??= FindCameraRay();
        if (RayCast == null) return;

        // Terrain gate: only interact when the ray hits a chunk. Edits go
        // through ChunkManager.SetBlock with GLOBAL positions now, so no
        // chunk-local math (and no chunk variable) is needed here anymore.
        if (!RayCast.IsColliding() || RayCast.GetCollider() is not Chunk) {
            if (BlockHighlight != null) BlockHighlight.Visible = false;
            return;
        }

        Vector3 hit = RayCast.GetCollisionPoint() - 0.5f * RayCast.GetCollisionNormal();
        Vector3I blockPos = new(Mathf.FloorToInt(hit.X), Mathf.FloorToInt(hit.Y), Mathf.FloorToInt(hit.Z));

        if (BlockHighlight != null) {
            BlockHighlight.Visible = true;
            BlockHighlight.GlobalPosition = blockPos + new Vector3(0.5f, 0.5f, 0.5f);
            // It's a child of the player, so it inherits the body's yaw -
            // cancel it or the box rotates off the voxel grid as you turn.
            BlockHighlight.GlobalRotation = Vector3.Zero;
        }

        // Component policy (BlockInteract): an equipped item needs the
        // component to touch blocks; empty hands keep the default.
        ItemDef equipped = Equipment?.Current;
        BlockInteract blockTool = equipped?.GetComponent<BlockInteract>();
        bool canMine = equipped == null || (blockTool?.CanMine ?? false);
        bool canPlace = equipped == null || (blockTool?.CanPlace ?? false);

        if (Input.IsActionJustPressed("destroy") && canMine) {
            Dictionary<string, object> mine = new() {
                ["position"] = blockPos,
                ["normal"] = RayCast.GetCollisionNormal(),
            };

            // Cancellable: a listener could protect regions (spawn, claims).
            if (!Events.Emit("block.mine", mine).Cancelled) {
                ChunkManager.Instance?.SetBlock(blockPos, BlockManager.Instance.Air);
            }
        }

        if (Input.IsActionJustPressed("interact") && canPlace) {
            // Which block gets placed is ITEM data now (BlockInteract
            // .PlacesBlockId -> Registries.Blocks); empty hands keep the
            // stone placeholder. The engine no longer names any block.
            string placeId = blockTool != null ? blockTool.PlacesBlockId : "origins:stone";
            Block toPlace = Registries.Blocks.Get(placeId);

            if (toPlace == null) {
                GD.PushWarning($"[player] block '{placeId}' is not registered - not placing");
                return;
            }

            Vector3I place = blockPos + (Vector3I)RayCast.GetCollisionNormal();

            Dictionary<string, object> placeData = new() {
                ["position"] = place,
                ["normal"] = RayCast.GetCollisionNormal(),
                ["block"] = placeId,
            };

            if (!Events.Emit("block.place", placeData).Cancelled) {
                ChunkManager.Instance?.SetBlock(place, toPlace);
            }
        }
    }

    private RayCast3D FindCameraRay() {
        Camera3D cam = GetViewport().GetCamera3D();
        return cam == null ? null : FindRayRecursive(cam);
    }

    private static RayCast3D FindRayRecursive(Node node) {
        foreach (Node child in node.GetChildren()) {
            if (child is RayCast3D ray) return ray;
            RayCast3D found = FindRayRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    private void TryStartAttack() {
        if (attacking) return;
        attacking = true;

        anim.Play(activeStance.AttackAnim);

        // Safety net: if the animation is missing, renamed or looping,
        // animation_finished never fires - time out instead of soft-locking.
        // (There is no GetAnimationOrNull - guard with HasAnimation.)
        Animation attackAnim = anim.HasAnimation(activeStance.AttackAnim)
            ? anim.GetAnimation(activeStance.AttackAnim)
            : null;
        attackTimeout = (attackAnim != null ? (float)attackAnim.Length : 0f) + 0.25f;
    }

    private void OnAnimationFinished(StringName animName) {
        if (animName == activeStance.AttackAnim) attacking = false;
    }

    private void TickAttackTimeout(float dt) {
        if (!attacking) return;
        attackTimeout -= dt;
        if (attackTimeout <= 0f) attacking = false;
    }

    private void UpdateAnimation() {
        if (attacking) return; // the swing owns the player until it finishes

        if (!IsOnFloor()) {
            anim.Play(activeStance.JumpAnim);
            return;
        }

        bool moving = new Vector2(Velocity.X, Velocity.Z).Length() > 0.1f;
        anim.Play(moving ? activeStance.WalkAnim : activeStance.IdleAnim);
    }
}
