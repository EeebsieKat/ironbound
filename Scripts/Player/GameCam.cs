using Godot;
using System;

public partial class GameCam : Node3D {

    [Export] public Node3D Target;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float HeightOffset = 1.6f;
    [Export] public float MinPitch = Mathf.DegToRad(-80f);
    [Export] public float MaxPitch = Mathf.DegToRad(30f);

    [Export] public string SkeletonPath = "FemaleMortalModel/ROOT/Skeleton3D";
    [Export] public string HeadBoneName = "Head";
    [Export] public string NeckBoneName = "Neck";
    [Export] public bool ModelFacesPositiveZ = false;
    [Export] public float MaxHeadYaw = Mathf.DegToRad(80f);
    [Export] public float MaxHeadPitch = Mathf.DegToRad(60f);
    [Export] public float HeadTurnSpeed = 15f;

    [Export(PropertyHint.Range, "0,1,0.05")] public float NeckYawShare = 0.4f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float NeckPitchShare = 0.4f;

    [Export] public string LeanBoneName = "Waist";
    [Export] public float MaxLeanForward = Mathf.DegToRad(8f);
    [Export] public float MaxLeanSide = Mathf.DegToRad(5f);
    [Export] public float LeanReferenceSpeed = 5f;
    [Export] public float LeanSmoothing = 8f;

    private Node3D cameraPitchNode;
    private Camera3D camera;
    private Skeleton3D skeleton;
    private CharacterBody3D body;
    private BoneLean bodyLean;
    private HeadLook neckLook;
    private HeadLook headLook;
    private float pitch = 0f;
    private float lookYaw = 0f;
    private float lookPitch = 0f;
    private float leanForward = 0f;
    private float leanSide = 0f;

    public override void _Ready() {
        Input.MouseMode = Input.MouseModeEnum.Captured;

        if (Target == null && GetParent() != null) {
            Target = GetParent().GetNodeOrNull<Node3D>("FemaleMortal");
        }

        cameraPitchNode = GetNodeOrNull<Node3D>("CameraPitch");
        camera = GetNodeOrNull<Camera3D>("CameraPitch/SpringArm3D/Camera3D");

        if (Target != null) {
            skeleton = Target.GetNodeOrNull<Skeleton3D>(SkeletonPath);
            if (skeleton != null) {
                bodyLean = new BoneLean();
                bodyLean.Name = "BodyLean";
                bodyLean.BoneName = LeanBoneName;
                skeleton.AddChild(bodyLean);

                neckLook = new HeadLook();
                neckLook.Name = "NeckLook";
                neckLook.BoneName = NeckBoneName;
                skeleton.AddChild(neckLook);

                headLook = new HeadLook();
                headLook.Name = "HeadLook";
                headLook.BoneName = HeadBoneName;
                skeleton.AddChild(headLook);
            }
        }
    }

    public override void _PhysicsProcess(double delta) {
        if (Target != null) {
            GlobalPosition = Target.GlobalPosition + new Vector3(0, HeightOffset, 0);
        }
    }

    public override void _Process(double delta) {
        UpdateLean(delta);
        UpdateHead(delta);
    }

    private void UpdateLean(double delta) {
        if (bodyLean == null || body == null || skeleton == null) return;

        Vector3 vel = body.Velocity;
        vel.Y = 0f;
        Vector3 v = skeleton.GlobalTransform.Basis.Orthonormalized().Inverse() * vel;

        Vector3 fwd = ModelFacesPositiveZ ? Vector3.Back : Vector3.Forward;
        Vector3 right = fwd.Cross(Vector3.Up);

        float fwdAmount = Mathf.Clamp(v.Dot(fwd) / LeanReferenceSpeed, -1f, 1f);
        float sideAmount = Mathf.Clamp(v.Dot(right) / LeanReferenceSpeed, -1f, 1f);

        float t = 1f - Mathf.Exp(-LeanSmoothing * (float)delta);
        leanForward = Mathf.Lerp(leanForward, fwdAmount * MaxLeanForward, t);
        leanSide = Mathf.Lerp(leanSide, sideAmount * MaxLeanSide, t);

        bodyLean.Offset = new Basis(right, -leanForward) * new Basis(fwd, leanSide);
    }
    
    private void UpdateHead(double delta) {
        if (neckLook == null || headLook == null || skeleton == null || camera == null) return;

        Vector3 dir = -camera.GlobalTransform.Basis.Z;
        Vector3 d = skeleton.GlobalTransform.Basis.Orthonormalized().Inverse() * dir;

        if (!ModelFacesPositiveZ) {
            d.X = -d.X;
            d.Z = -d.Z;
        }
        d = d.Normalized();

        float pitchSign = ModelFacesPositiveZ ? -1f : 1f;

        float targetYaw = Mathf.Atan2(d.X, d.Z);
        float targetPitch = pitchSign * Mathf.Asin(Mathf.Clamp(d.Y, -1f, 1f));

        targetYaw = Mathf.Clamp(targetYaw, -MaxHeadYaw, MaxHeadYaw);
        targetPitch = Mathf.Clamp(targetPitch, -MaxHeadPitch, MaxHeadPitch);

        float t = 1f - Mathf.Exp(-HeadTurnSpeed * (float)delta);
        lookYaw = Mathf.LerpAngle(lookYaw, targetYaw, t);
        lookPitch = Mathf.Lerp(lookPitch, targetPitch, t);

        neckLook.Yaw = lookYaw * NeckYawShare;
        neckLook.Pitch = lookPitch * NeckPitchShare;

        headLook.Yaw = lookYaw * (1f - NeckYawShare);
        headLook.Pitch = lookPitch * (1f - NeckPitchShare);
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

        if (@event is InputEventMouseMotion mm) {
            RotateY(-mm.Relative.X * MouseSensitivity);

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