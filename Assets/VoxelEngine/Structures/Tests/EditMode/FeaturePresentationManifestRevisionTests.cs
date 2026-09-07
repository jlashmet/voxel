using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FeaturePresentationManifestRevisionTests
    {
        [Test]
        public void RevisionTracksQueryMembershipAndBakeReplacement()
        {
            var manifest=new FeaturePresentationManifest();
            var bounds=new FeaturePresentationBounds(new int3(-100),new int3(100));
            var first=Bake(1);manifest.Upsert(first);
            ulong version=manifest.Revision;
            Assert.AreSame(first,manifest.Query(bounds)[0]);
            var edited=Bake(2);manifest.Upsert(edited);
            Assert.Greater(manifest.Revision,version);
            Assert.AreSame(edited,manifest.Query(bounds)[0]);
            version=manifest.Revision;
            Assert.IsFalse(manifest.Remove(2));Assert.AreEqual(version,manifest.Revision);
            Assert.IsTrue(manifest.Remove(1));Assert.Greater(manifest.Revision,version);
            Assert.AreEqual(0,manifest.Query(bounds).Count);
        }
        private static FeaturePresentationBake Bake(ulong revision)=>new(1,revision,default,int3.zero,0,
            int3.zero,new int3(10),new[]{new Primitive {Shape=PrimitiveShape.Box,Mode=PrimitiveMode.Fill,
                A=int3.zero,B=new int3(10),Material=1}});
    }
}
