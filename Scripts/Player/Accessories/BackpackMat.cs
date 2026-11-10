using Godot;
using System;
using System.Threading.Tasks;

public partial class BackpackMat : Node3D {
	public AnimationPlayer anim;
	public Boolean isOpen = false;
	public override void _Ready() {
		this.Visible = false;

		anim = GetNode<AnimationPlayer>("AnimationPlayer");
		anim.Play("open");
	}

	public override void _Process(double delta) {
	}

    public override void _UnhandledInput(InputEvent @event) {
        if (@event.IsActionPressed("inventory")) {
            _ = OpenBackpackMat();
		}
    }

	public async Task OpenBackpackMat() {
		isOpen = !isOpen;
			if (isOpen) {
				this.Visible = true;
				anim.Play("unfold");
			} else {
				anim.Play("fold");

				await ToSignal(anim, "animation_finished");
				
				if (!isOpen) {
					this.Visible = false;
				}
			}
    }
}
