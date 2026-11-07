using Godot;

public partial class BoneLean : SkeletonModifier3D {

    public string BoneName = "Waist";
    public Basis Offset = Basis.Identity;

    private int boneIdx = -1;

    public override void _ProcessModification() {
        Skeleton3D skel = GetSkeleton();
        if (skel == null) return;

        if (boneIdx < 0) {
            boneIdx = skel.FindBone(BoneName);
            if (boneIdx < 0) return;
        }

        Transform3D global = skel.GetBoneGlobalPose(boneIdx);
        global.Basis = Offset * global.Basis;

        int parent = skel.GetBoneParent(boneIdx);
        Transform3D parentGlobal = parent >= 0 ? skel.GetBoneGlobalPose(parent) : Transform3D.Identity;
        Transform3D local = parentGlobal.AffineInverse() * global;

        skel.SetBonePoseRotation(boneIdx, local.Basis.GetRotationQuaternion());
    }
}