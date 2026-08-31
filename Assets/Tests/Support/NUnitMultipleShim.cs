namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Unity ships com.unity.ext.nunit 2.1.0, a custom build of NUnit 3.5. Assert.Multiple arrived in
    /// NUnit 3.6, so tests cannot call it directly. Declaring Assert here shadows NUnit.Framework.Assert
    /// for the shared test namespace; Multiple simply runs the block and preserves fail-fast semantics.
    /// Delete this shim if the project moves to NUnit 3.6 or newer.
    /// </summary>
    public class Assert : NUnit.Framework.Assert
    {
        public static void Multiple(NUnit.Framework.TestDelegate block) => block();
    }
}
