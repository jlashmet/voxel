using Game.Application.Api;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeMacroWorldApplicationStartDriverTests
    {
        [TestCase(ApplicationLifecycle.Boot, ApplicationScreen.Boot, false, false)]
        [TestCase(ApplicationLifecycle.FrontEnd, ApplicationScreen.MainMenu, false, true)]
        [TestCase(ApplicationLifecycle.FrontEnd, ApplicationScreen.MainMenu, true, false)]
        [TestCase(ApplicationLifecycle.FrontEnd, ApplicationScreen.Settings, false, false)]
        [TestCase(ApplicationLifecycle.StartingSession, ApplicationScreen.Loading, false, false)]
        [TestCase(ApplicationLifecycle.InGame, ApplicationScreen.Gameplay, false, false)]
        public void RequestsNewGameOnlyFromUnclaimedFrontEndMainMenu(
            ApplicationLifecycle lifecycle,
            ApplicationScreen screen,
            bool alreadyRequested,
            bool expected)
        {
            var flow = new ApplicationFlowSnapshot(
                lifecycle,
                screen,
                gameplayReady: false,
                ApplicationFailure.None,
                string.Empty);

            Assert.That(
                KentridgeMacroWorldApplicationStartDriver.ShouldRequestNewGame(flow, alreadyRequested),
                Is.EqualTo(expected));
        }

        [TestCase(0f, true)]
        [TestCase(4.99f, true)]
        [TestCase(5f, false)]
        [TestCase(30f, false)]
        public void LateValidationProfileGetsBoundedEvidenceDiscoveryWindow(
            float elapsedSeconds,
            bool expected)
        {
            Assert.That(
                KentridgeMacroWorldApplicationStartDriver.ShouldAwaitEvidence(elapsedSeconds),
                Is.EqualTo(expected));
        }

        [Test]
        public void DontSaveSceneIssueEvidenceIsFoundByValidationSafeDiscovery()
        {
            Assert.That(
                KentridgeMacroWorldApplicationStartDriver.HasActiveValidationEvidence(),
                Is.False,
                "Test requires no pre-existing macro validation evidence instance.");

            float originalTimeScale = Time.timeScale;
            var host = new GameObject("Hidden macro evidence test")
            {
                hideFlags = HideFlags.DontSave
            };
            try
            {
                host.AddComponent<KentridgeMacroWorldEvidenceDriver>();
                Assert.That(
                    KentridgeMacroWorldApplicationStartDriver.HasActiveValidationEvidence(),
                    Is.True,
                    "DontSave SceneIssue evidence must remain discoverable by the application-start companion.");

                host.SetActive(false);
                Assert.That(
                    KentridgeMacroWorldApplicationStartDriver.HasActiveValidationEvidence(),
                    Is.False,
                    "Disabled validation evidence must not trigger application startup.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Time.timeScale = originalTimeScale;
            }
        }
    }
}
