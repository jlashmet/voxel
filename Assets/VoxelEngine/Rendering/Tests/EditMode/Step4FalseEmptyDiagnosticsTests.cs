using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class Step4FalseEmptyDiagnosticsTests
    {
        [Test]
        public void ReadyEmptyPublicationAnchorsFallbackGuardInputs()
        {
            Step4FalseEmptyDiagnostics.Reset();
            MethodInfo record = typeof(Step4FalseEmptyDiagnostics).GetMethod(
                "RecordReadyEmptyPublication",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(record);

            record.Invoke(null, new object[] { new int3(1, 2, 3), false, false, false });
            record.Invoke(null, new object[] { new int3(4, 5, 6), true, true, false });
            record.Invoke(null, new object[] { new int3(7, 8, 9), true, false, true });

            Step4FalseEmptyDiagnostics.Snapshot snapshot = Step4FalseEmptyDiagnostics.Current;
            Assert.AreEqual(3, snapshot.ReadyEmptyPublications);
            Assert.AreEqual(2, snapshot.ReadyEmptyOwnedSolid);
            Assert.AreEqual(1, snapshot.ReadyEmptyUnowned);
            Assert.AreEqual(1, snapshot.ReadyEmptyWithProfiles);
            Assert.AreEqual(1, snapshot.ReadyEmptyUsedFallback);

            string text = snapshot.ToString();
            StringAssert.Contains("readyEmptyOwned:2", text);
            StringAssert.Contains("readyEmptyUnowned:1", text);
            StringAssert.Contains("readyEmptyProfiles:1", text);
            StringAssert.Contains("readyEmptyUsedFallback:1", text);
        }
    }
}
