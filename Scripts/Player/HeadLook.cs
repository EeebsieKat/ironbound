using Godot;

// Applies a yaw/pitch rotation to one bone on top of whatever the animation
// posed, as a SkeletonModifier3D (Godot 4.3+). Being a modifier means it runs
// inside the skeleton's update, AFTER the AnimationPlayer applies its poses,
// so there is no fighting or one-frame lag with walk/idle animations.
//
// Convention (values are fed by GameCam):
//   Yaw   + = turn left (rotation about the skeleton's up axis)
//   Pitch + = look up   (rotation about the model's right axis)
// The rotation happens about the bone's own joint, in skeleton space.
public partial class HeadLook : SkeletonModifier3D {

    [Export] public string BoneName = "Head";
    [Export] public bool ModelFacesPositiveZ = false;

    public float Yaw { get; set; }
    public float Pitch { get; set; }

    private Skeleton3D skeleton;
    private int bone = -1;

    public override void _Ready() {
        skeleton = GetParent() as Skeleton3D;
    }

    public override void _ProcessModificationWithDelta(double delta) {
        if (skeleton == null) return;
        if (bone < 0) bone = skeleton.FindBone(BoneName);
        if (bone < 0) return; // bone missing in this rig: no-op, no errors

        Vector3 fwd = ModelFacesPositiveZ ? Vector3.Back : Vector3.Forward;
        Vector3 right = fwd.Cross(Vector3.Up);

        Basis offset = new Basis(Vector3.Up, Yaw) * new Basis(right, Pitch);

        Transform3D pose = skeleton.GetBoneGlobalPose(bone);
        skeleton.SetBoneGlobalPose(bone, new Transform3D(offset * pose.Basis, pose.Origin));
    }
}
