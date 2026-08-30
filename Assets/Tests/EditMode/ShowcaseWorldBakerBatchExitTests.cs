using System;
using System.Reflection;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseWorldBakerBatchExitTests
    {
        [Test]
        public void SuccessfulBatchBakeRequestsZeroExit()
        {
            MethodInfo completion = RequireCompletionMethod();
            int? requestedExitCode = null;
            Action<int> exit = code => requestedExitCode = code;

            completion.Invoke(null, new object[] { true, exit });

            Assert.That(requestedExitCode, Is.EqualTo(0));
        }

        [Test]
        public void SuccessfulInteractiveBakeDoesNotRequestExit()
        {
            MethodInfo completion = RequireCompletionMethod();
            int exitCalls = 0;
            Action<int> exit = _ => exitCalls++;

            completion.Invoke(null, new object[] { false, exit });

            Assert.That(exitCalls, Is.Zero);
        }

        private static MethodInfo RequireCompletionMethod()
        {
            Type bakerType = Type.GetType(
                "VoxelEngine.Showcase.Editor.ShowcaseWorldBaker, VoxelEngine.Showcase.Editor");
            Assert.That(bakerType, Is.Not.Null, "Showcase baker Editor assembly must be loadable.");

            MethodInfo completion = bakerType.GetMethod(
                "CompleteSuccessfulBake",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(completion, Is.Not.Null, "Successful-bake completion hook must remain testable.");
            return completion;
        }
    }
}
