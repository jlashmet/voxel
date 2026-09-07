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
    }
}
