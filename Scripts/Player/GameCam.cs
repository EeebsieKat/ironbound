using Godot;

// Third-person orbit rig + procedural head look and body lean.
//
// Fixes over the original:
//  - `body` was never assigned, so UpdateLean() returned on its first line and
//    the lean system silently never ran. It is assigned now.
//  - head aim math rewritten without the X/Z flipping: yaw/pitch are derived
//    directly from the look direction in skeleton space (see convention below).
//  - added wheel zoom, shoulder offset, and Esc toggles mouse capture.
//
// The HeadLook/BodyLean nodes are created here as SkeletonModifier3D children
// of the skeleton (Godot 4.3+), so they are applied automatically AFTER the
// AnimationPlayer poses the bones - no fighting the walk cycle.
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

    [ExportGroup("Zoom / Framing")]
    [Export] public float MinZoom = 1.5f;
    [Export] public float MaxZoom = 8f;
    [Export] public float ZoomStep = 0.5f;
    [Export] public float ZoomSmoothing = 10f;
    [Export] public float ShoulderOffset = 0f;

    private Node3D cameraPitchNode;
    private SpringArm3D springArm;
    private Camera3D camera;
    private Skeleton3D skeleton;
    private CharacterBody3D body;
    private BodyLean bodyLean;
    private HeadLook neckLook;
    private HeadLook headLook;
    private float pitch = 0f;
    private float lookYaw = 0f;
    private float lookPitch = 0f;
    private float leanForward = 0f;
    private float leanSide = 0f;
    private float targetZoom = 4f;

    public override void _Ready() {
        Input.MouseMode = Input.MouseModeEnum.Captured;

        if (Target == null && GetParent() != null) {
            Target = GetParent().GetNodeOrNull<Node3D>("FemaleMortal");
        }

        cameraPitchNode = GetNodeOrNull<Node3D>("CameraPitch");
        springArm = GetNodeOrNull<SpringArm3D>("CameraPitch/SpringArm3D");
        camera = GetNodeOrNull<Camera3D>("CameraPitch/SpringArm3D/Camera3D");

        if (springArm != null) {
            targetZoom = springArm.SpringLength;
        } else if (camera != null) {
            // No SpringArm3D in the rig: start from the camera's actual distance.
            targetZoom = Mathf.Clamp(camera.Position.Length(), MinZoom, MaxZoom);
        }

        // FIX: this was never assigned before, so lean bailed out instantly.
        body = Target as CharacterBody3D;
        if (body == null && Target != null) {
            body = Target.GetParent() as CharacterBody3D;
        }

        if (Target != null) {
            skeleton = Target.GetNodeOrNull<Skeleton3D>(SkeletonPath);
            if (skeleton != null) {
                bodyLean = new BodyLean {
                    Name = "BodyLean",
                    BoneName = LeanBoneName,
                    ModelFacesPositiveZ = ModelFacesPositiveZ,
                };
                skeleton.AddChild(bodyLean);

                neckLook = new HeadLook {
                    Name = "NeckLook",
                    BoneName = NeckBoneName,
                    ModelFacesPositiveZ = ModelFacesPositiveZ,
                };
                skeleton.AddChild(neckLook);

                headLook = new HeadLook {
                    Name = "HeadLook",
                    BoneName = HeadBoneName,
                    ModelFacesPositiveZ = ModelFacesPositiveZ,
                };
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
        float t = 1f - Mathf.Exp(-ZoomSmoothing * (float)delta);

        if (springArm != null) {
            // Zoom the arm's reach - wall collision still retracts the camera.
            springArm.SpringLength = Mathf.Lerp(springArm.SpringLength, targetZoom, t);
        } else if (camera != null) {
            // Rig without a SpringArm3D: move the camera along its local Z.
            // Sign matches however the camera is offset in the scene.
            float sign = camera.Position.Z >= 0f ? 1f : -1f;
            Vector3 pos = camera.Position;
            pos.Z = Mathf.Lerp(pos.Z, sign * targetZoom, t);
            camera.Position = pos;
        }

        if (cameraPitchNode != null) {
            cameraPitchNode.Position = new Vector3(ShoulderOffset, 0, 0);
        }

        UpdateLean(delta);
        UpdateHead(delta);
    }

    private void UpdateLean(double delta) {
        if (bodyLean == null || body == null || skeleton == null) return;

        // Horizontal velocity expressed in the skeleton's local space.
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

        bodyLean.LeanForward = leanForward;
        bodyLean.LeanSide = leanSide;
    }

    private void UpdateHead(double delta) {
        if (neckLook == null || headLook == null || skeleton == null || camera == null) return;

        // Where the camera looks, expressed in skeleton space.
        Vector3 dir = -camera.GlobalTransform.Basis.Z;
        Vector3 d = skeleton.GlobalTransform.Basis.Orthonormalized().Inverse() * dir;

        Vector3 fwd = ModelFacesPositiveZ ? Vector3.Back : Vector3.Forward;

        Vector3 horiz = new Vector3(d.X, 0f, d.Z);
        if (horiz.LengthSquared() < 0.0001f) {
            horiz = fwd; // looking straight up/down: keep yaw neutral
        } else {
            horiz = horiz.Normalized();
        }

        // HeadLook convention: yaw + = turn left (about skeleton up),
        // pitch + = look up (about the model's right axis).
        // Signed rotation about Up that takes the model forward onto horiz:
        float yawRaw = Mathf.Atan2(fwd.Cross(horiz).Dot(Vector3.Up), fwd.Dot(horiz));
        float pitchRaw = Mathf.Asin(Mathf.Clamp(d.Normalized().Y, -1f, 1f));

        float targetYaw = Mathf.Clamp(yawRaw, -MaxHeadYaw, MaxHeadYaw);
        float targetPitch = Mathf.Clamp(pitchRaw, -MaxHeadPitch, MaxHeadPitch);

        float t = 1f - Mathf.Exp(-HeadTurnSpeed * (float)delta);
        lookYaw = Mathf.LerpAngle(lookYaw, targetYaw, t);
        lookPitch = Mathf.Lerp(lookPitch, targetPitch, t);

        neckLook.Yaw = lookYaw * NeckYawShare;
        neckLook.Pitch = lookPitch * NeckPitchShare;

        headLook.Yaw = lookYaw * (1f - NeckYawShare);
        headLook.Pitch = lookPitch * (1f - NeckPitchShare);
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (@event.IsActionPressed("ui_cancel")) {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed) {
            if (mb.ButtonIndex == MouseButton.WheelUp) {
                targetZoom = Mathf.Max(MinZoom, targetZoom - ZoomStep);
            } else if (mb.ButtonIndex == MouseButton.WheelDown) {
                targetZoom = Mathf.Min(MaxZoom, targetZoom + ZoomStep);
            }
            return;
        }

        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

        if (@event is InputEventMouseMotion mm) {
            RotateY(-mm.Relative.X * MouseSensitivity);

            if (cameraPitchNode != null) {
                pitch = Mathf.Clamp(pitch - mm.Relative.Y * MouseSensitivity, MinPitch, MaxPitch);
                cameraPitchNode.Rotation = new Vector3(pitch, 0, 0);
            }
        }
    }
}
