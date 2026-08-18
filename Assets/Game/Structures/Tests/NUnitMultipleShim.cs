namespace Game.Structures.Tests
{
    /// <summary>
    /// Unity ships com.unity.ext.nunit 2.1.0, a custom build of NUnit 3.5. Assert.Multiple arrived in
    /// NUnit 3.6, so the tests in this assembly cannot call it. Declaring Assert here shadows
    /// NUnit.Framework.Assert for this namespace: every other assertion is inherited unchanged, and
    /// Multiple simply runs the block.
    ///
    /// The one behavioural difference from real Assert.Multiple: failures are not collected, so the
    /// first failing assertion inside the block aborts the rest instead of reporting them together.
    /// A failing test still fails. Delete this file if the project ever moves to NUnit 3.6 or newer.
    /// </summary>
    public class Assert : NUnit.Framework.Assert
    {
        public static void Multiple(NUnit.Framework.TestDelegate block) => block();
    }
}
