using Godot;

// Leans the waist bone into movement, on top of the animated pose
// (SkeletonModifier3D, Godot 4.3+ - applied after the AnimationPlayer).
//
// Convention (values are fed by GameCam):
//   LeanForward + = tip forward into forward motion
//   LeanSide    + = lean right into rightward motion
// Same rotation math as the original GameCam lean, just moved next to the
// bone and ordered correctly relative to the animation.
public partial class BodyLean : SkeletonModifier3D {

    [Export] public string BoneName = "Waist";
    [Export] public bool ModelFacesPositiveZ = false;

    public float LeanForward { get; set; }
    public float LeanSide { get; set; }

    private Skeleton3D skeleton;
    private int bone = -1;

    public override void _Ready() {
        skeleton = GetParent() as Skeleton3D;
    }

    public override void _ProcessModificationWithDelta(double delta) {
        if (skeleton == null) return;
        if (bone < 0) bone = skeleton.FindBone(BoneName);
        if (bone < 0) return;

        Vector3 fwd = ModelFacesPositiveZ ? Vector3.Back : Vector3.Forward;
        Vector3 right = fwd.Cross(Vector3.Up);

        Basis offset = new Basis(right, -LeanForward) * new Basis(fwd, LeanSide);

        Transform3D pose = skeleton.GetBoneGlobalPose(bone);
        skeleton.SetBoneGlobalPose(bone, new Transform3D(offset * pose.Basis, pose.Origin));
    }
}
