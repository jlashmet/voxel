using System;
using System.Reflection;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseWorldBakerBatchExitTests
    {
        private const string BakeExecuteMethod =
            "VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld";

        [Test]
        public void ExactGithubActionsBatchBakeRequestsZeroExit()
        {
            int? requestedExitCode = InvokeCompletion(
                true,
                "true",
                "Unity",
                "-batchmode",
                "-nographics",
                "-quit",
                "-executeMethod",
                BakeExecuteMethod);

            Assert.That(requestedExitCode, Is.EqualTo(0));
        }

        [Test]
        public void ExitPolicyRejectsInteractiveNonCiTestsAndOtherExecuteMethods()
        {
            Assert.That(
                InvokeCompletion(
                    false,
                    "true",
                    "Unity",
                    "-batchmode",
                    "-executeMethod",
                    BakeExecuteMethod),
                Is.Null,
                "Interactive editor bakes must never terminate the editor.");

            Assert.That(
                InvokeCompletion(
                    true,
                    null,
                    "Unity",
                    "-batchmode",
                    "-executeMethod",
                    BakeExecuteMethod),
                Is.Null,
                "Local/non-CI batch bakes must return normally.");

            Assert.That(
                InvokeCompletion(
                    true,
                    "false",
                    "Unity",
                    "-batchmode",
                    "-executeMethod",
                    BakeExecuteMethod),
                Is.Null,
                "A false GitHub Actions marker must not opt into process termination.");

            Assert.That(
                InvokeCompletion(
                    true,
                    "true",
                    "Unity",
                    "-batchmode",
                    "-runTests",
                    "-executeMethod",
                    BakeExecuteMethod),
                Is.Null,
                "Unity test-runner invocations must retain normal lifecycle ownership.");

            Assert.That(
                InvokeCompletion(
                    true,
                    "true",
                    "Unity",
                    "-batchmode",
                    "-executeMethod",
                    "VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeWorldbuildingGalleryWorld"),
                Is.Null,
                "Other execute methods must not inherit the showcase CI shutdown policy.");

            Assert.That(
                InvokeCompletion(true, "true", "Unity", "-batchmode", "-quit"),
                Is.Null,
                "A generic GitHub Actions batch process must not be terminated by the baker policy.");
        }

        private static int? InvokeCompletion(
            bool isBatchMode,
            string githubActions,
            params string[] commandLineArgs)
        {
            MethodInfo completion = RequireCompletionMethod();
            int? requestedExitCode = null;
            Action<int> exit = code => requestedExitCode = code;

            completion.Invoke(
                null,
                new object[] { isBatchMode, githubActions, commandLineArgs, exit });
            return requestedExitCode;
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
