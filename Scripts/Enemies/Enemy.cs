using Godot;
using Godot.Collections;
using System;
using System.Security.Cryptography.X509Certificates;

public partial class Enemy : CharacterBody3D {

	public float SPEED = 4.0f;
	public float ATTACK_RANGE = 10.0f;
	public float JUMP_VEL = 2.0f;
	
	LevelManager levelManager;
	public AnimationPlayer anim;
	public Node3D target;

	public float GRAVITY = 9.8f * 2f;

	public float rotation_direction = 0.0f;

	public Vector3 velocity;

	public float HEALTH =  100f;

	public enum STATE{
		IDLE,
		ATTACK,
		STRUGGLE,
		DIE
	}

	public STATE currentState;
	public override void _Ready() {
		currentState = STATE.IDLE;

		levelManager = GetTree().Root.GetNode<LevelManager>("LevelManager");
		target = GetParent().GetParent().GetNode<Node3D>("FemaleMortal");
		anim = GetNode<AnimationPlayer>("FemaleMortalModel/AnimationPlayer");
	}

	public override void _PhysicsProcess(double delta) {
        switch(currentState){
			case STATE.IDLE:
				UpdateIdle();
				break;
			case STATE.ATTACK:
				UpdateAttack(delta);
				break;
			case STATE.DIE:
				UpdateDie();
				break;
		}
		velocity = Velocity;
		if (!IsOnFloor())
			velocity.Y -= GRAVITY * (float)delta;

		if (new Vector2(Velocity.X,Velocity.Z).Length() > 0.0){
			rotation_direction = new Vector2(Velocity.Z, Velocity.X).Angle();
			Vector3 rot = Rotation;
			rot.Y = (float)(Mathf.LerpAngle(Rotation.Y, rotation_direction, delta * 10));
			Rotation = rot;
		}
		Velocity = velocity;
		Animate();
    }

	private void UpdateIdle(){
		if (Position.DistanceTo(target.Position) < ATTACK_RANGE) {
			currentState = STATE.ATTACK;
		}
	}

	private void UpdateAttack(double delta) {
		Vector3 t = target.Position;
		t.Y = Position.Y;
		Vector3 direction = Position.DirectionTo(t);
		velocity = Velocity;
		velocity.X = direction.X * SPEED;
		velocity.Z = direction.Z * SPEED;
		Velocity = velocity;
		MoveAndSlide();
	}

	public void Animate(){
		if(new Vector2(Velocity.X,Velocity.Z).Length() > 0.0){
			anim.Play("walk");

		}else{
			anim.Play("idle");
		}
	}

	public void UpdateDie() {
		levelManager.AddScore(1);
		QueueFree();
	}

	public void Hit() {
		HEALTH -= 25f;
		GD.Print("Enemy Health: ", HEALTH);
		if (HEALTH <= 0) {
			currentState = STATE.DIE;
		}
	}
}
