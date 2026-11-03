using Godot;
using System;

public partial class GameCam : Node3D {

    [Export] public Node3D Target;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float HeightOffset = 1.6f; // Height adjustment to target chest/head level
    [Export] public float MinPitch = Mathf.DegToRad(-80f);
    [Export] public float MaxPitch = Mathf.DegToRad(60f);

    private Node3D cameraPitchNode;
    private float pitch = 0f;

    public override void _Ready() {
        Input.MouseMode = Input.MouseModeEnum.Captured;

        if (Target == null && GetParent() != null) {
            Target = GetParent().GetNodeOrNull<Node3D>("FemaleMortal");
        }

        // Grab the CameraPitch child node for clean vertical rotation
        cameraPitchNode = GetNodeOrNull<Node3D>("CameraPitch");
    }

    public override void _PhysicsProcess(double delta) {
        if (Target != null) {
            // Follow player position plus an upward height offset
            GlobalPosition = Target.GlobalPosition + new Vector3(0, HeightOffset, 0);
        }
    }

    public override void _UnhandledInput(InputEvent @event) {
        // Stop rotating if inventory/UI is open and mouse is free
        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

        if (@event is InputEventMouseMotion mm) {
            // 1. Horizontal Rotation (Yaw) -> Rotates the main GameCam parent node left/right
            RotateY(-mm.Relative.X * MouseSensitivity);

            // 2. Vertical Rotation (Pitch) -> Rotates the CameraPitch node up/down
            if (cameraPitchNode != null) {
                pitch -= mm.Relative.Y * MouseSensitivity;
                pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);

                Vector3 rot = cameraPitchNode.Rotation;
                rot.X = pitch;
                cameraPitchNode.Rotation = rot;
            }
        }
    }
}