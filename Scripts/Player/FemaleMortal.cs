using Godot;
using System;
using System.Threading.Tasks;

public partial class FemaleMortal : CharacterBody3D {

	[Export] public float SPEED = 5f;
	[Export] public float JUMP_VEL = 7f;
	[Export] public float GRAVITY = 9.8f * 2f;

	[Export] public float SWING_DECELERATION = 8.0f;

	public float rotation_angle = 0.0f;

	public AnimationPlayer anim;
	public Area3D swordArea;

	public override void _Ready() {
		anim = GetNode<AnimationPlayer>("FemaleMortalModel/AnimationPlayer");
		swordArea = GetNode<Area3D>("FemaleMortalModel/ROOT/Skeleton3D/SwordAttachment/Sword/SwordArea");
	}

	public override void _Process(double delta) {
		Vector3 velocity = Velocity;

		if (swordArea.Monitoring) {
            velocity.X = Mathf.Lerp(velocity.X, 0f, (float)(SWING_DECELERATION * delta));
            velocity.Z = Mathf.Lerp(velocity.Z, 0f, (float)(SWING_DECELERATION * delta));
        } else {
            Vector2 input = Input.GetVector("left", "right", "up", "down");
            Camera3D camera = GetViewport().GetCamera3D();

            if (camera != null) {
                Vector3 camForward = -camera.GlobalTransform.Basis.Z;
                Vector3 camRight = camera.GlobalTransform.Basis.X;

                camForward.Y = 0;
                camForward = camForward.Normalized();

                camRight.Y = 0;
                camRight = camRight.Normalized();
				
                Vector3 direction = (camForward * -input.Y) + (camRight * input.X);

                velocity.X = direction.X * SPEED;
                velocity.Z = direction.Z * SPEED;
            }
        }

		if (!IsOnFloor()) {
            velocity.Y -= GRAVITY * (float)delta;
        } else if (!swordArea.Monitoring) { 
            if (Input.IsActionJustPressed("jump")) {
				velocity.Y = JUMP_VEL;
			}
		}

		Velocity = velocity;

		if (!swordArea.Monitoring && new Vector2(Velocity.X, Velocity.Z).Length() > 0) {
			rotation_angle = new Vector2(Velocity.Z, Velocity.X).Angle();
			Vector3 rot = Rotation;

			rot.Y = (float)Mathf.LerpAngle(rot.Y, rotation_angle, 10 * delta);

			Rotation = rot;
		}

		MoveAndSlide();
		Animate();
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event.IsActionPressed("destroy") && !swordArea.Monitoring) {
			_ = PerformAttack();
		}
	}

	public void Animate() {
		if (swordArea.Monitoring) {
			anim.Play("attack");
			return;
		}

		if (IsOnFloor()) {
			if (new Vector2(Velocity.X, Velocity.Z).Length() > 0.1f) {
				anim.Play("walk");
			} else {
				anim.Play("idle");
			}
		} else {
			anim.Play("jump");
		}
	}

	public async Task PerformAttack() {
		swordArea.Monitoring = true;
		await ToSignal(anim, "animation_finished");
		swordArea.Monitoring = false;
	}

	public void HitEnemy(Enemy body) {
		if (body.HasMethod("Hit")) {
			body.Hit();
		}
	}
}
