using Godot;

public partial class HeadLook : SkeletonModifier3D {

    public string BoneName = "Head";
    public float Yaw = 0f;
    public float Pitch = 0f;

    private int boneIdx = -1;

    public override void _ProcessModification() {
        Skeleton3D skel = GetSkeleton();
        if (skel == null) return;

        if (boneIdx < 0) {
            boneIdx = skel.FindBone(BoneName);
            if (boneIdx < 0) return;
        }

        Transform3D global = skel.GetBoneGlobalPose(boneIdx);

        Basis offset = new Basis(Vector3.Up, Yaw) * new Basis(Vector3.Right, Pitch);
        global.Basis = offset * global.Basis;

        int parent = skel.GetBoneParent(boneIdx);
        Transform3D parentGlobal = parent >= 0 ? skel.GetBoneGlobalPose(parent) : Transform3D.Identity;
        Transform3D local = parentGlobal.AffineInverse() * global;

        skel.SetBonePoseRotation(boneIdx, local.Basis.GetRotationQuaternion());
    }
}