using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NewHouseReferenceFinishPassOrderTests
    {
        [NUnit.Framework.Test]
        public void FinishPass_RestoresUpperGableFlowerBoxAfterDestructivePortraitRebuild()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(projectRoot,
                "Assets/Game/WorldBuilder/Voxel/NewHouseReferenceFinishPass.cs");
            string source = File.ReadAllText(sourcePath);

            int rebuild = source.IndexOf("RebuildSweptPortraitShell(a, o, in c, in p);");
            int arch = source.IndexOf("ArchedPanel(a, centre, eave + 12, front, 11, 17", rebuild);
            int flowerBox = source.IndexOf(
                "AddDenseFlowerBox(a, centre - 12, portraitEave + 7, front - 2, 24, in p);",
                arch);

            Assert.That(rebuild, Is.GreaterThanOrEqualTo(0));
            Assert.That(arch, Is.GreaterThan(rebuild),
                "The final high portrait opening must be restored after the destructive swept-shell rebuild.");
            Assert.That(flowerBox, Is.GreaterThan(arch),
                "The upper portrait flower box must be restored after the final arch carve; otherwise the destructive finish pass erases the reference-defining planting.");
            Assert.That(source, Does.Contain("new int3(width - 2, 4, 4), p.Foliage"));
            Assert.That(source, Does.Contain("? p.Accent : p.Flowers"));
        }

        [NUnit.Framework.Test]
        public void FinishPass_ExtendsReferenceChimneyAfterGableUsingRidgeRelativeStoneStack()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(projectRoot,
                "Assets/Game/WorldBuilder/Voxel/NewHouseReferenceFinishPass.cs");
            string source = File.ReadAllText(sourcePath);

            int gable = source.IndexOf("RefinePortraitGable(a, o, in c, in p);");
            int chimney = source.IndexOf("ExtendReferenceChimney(a, o, in c, in p);", gable);
            int method = source.IndexOf("private static void ExtendReferenceChimney", chimney);
            int ridgeTop = source.IndexOf("int top = math.max(baseY + 12, ridge - 10);", method);
            int shaft = source.IndexOf(
                "new int3(10, top - baseY, 10), p.Stone", ridgeTop);
            int cap = source.IndexOf(
                "new int3(14, 3, 14), p.Stone", shaft);

            Assert.That(gable, Is.GreaterThanOrEqualTo(0));
            Assert.That(chimney, Is.GreaterThan(gable),
                "The reference chimney must be extended in the late finish pass after the final portrait-gable geometry is established.");
            Assert.That(method, Is.GreaterThan(chimney));
            Assert.That(ridgeTop, Is.GreaterThan(method),
                "The chimney top must remain tied to the house ridge rather than a fixed world-space height.");
            Assert.That(shaft, Is.GreaterThan(ridgeTop),
                "The late chimney correction must author a tall stone shaft, not only another cap primitive.");
            Assert.That(cap, Is.GreaterThan(shaft),
                "The tall shaft must finish with a restrained stepped masonry cap.");
        }
    }
}
