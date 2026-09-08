using Game.Application.Api;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;

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
    }
}
