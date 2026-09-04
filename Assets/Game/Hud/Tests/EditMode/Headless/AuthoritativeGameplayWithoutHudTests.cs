using System;
using System.Linq;
using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;

namespace Game.Hud.HeadlessRegression.Tests
{
    public sealed class AuthoritativeGameplayWithoutHudTests
    {
        [Test]
        public void SessionAndVitalityProgressWithoutHudAssemblyDependency()
        {
            var sessionId = new GameSessionId("headless-session");
            var config = new SessionStartupConfiguration(4, "test-protocol", "test-content", true);
            var session = new PartySession(sessionId, config);

            JoinResult joined = session.Join(new JoinRequest(sessionId, "player-a", "test-protocol", "test-content"));
            Assert.That(joined.Accepted, Is.True);
            PartyMemberId memberId = joined.Member.MemberId;
            var connection = new TransportConnectionHandle("connection-a");
            Assert.That(session.BindConnection(memberId, connection), Is.True);

            var characterId = new CharacterId("character-a");
            Assert.That(session.BindCharacter(memberId, characterId), Is.True);
            Assert.That(session.MarkSynchronized(memberId), Is.True);
            Assert.That(session.MarkGameplayReady(memberId), Is.True);
            Assert.That(session.StartGameplay(), Is.True);
            Assert.That(session.TryGetMember(memberId, out PartyMemberSnapshot member), Is.True);
            Assert.That(member.CharacterId, Is.EqualTo(characterId));
            Assert.That(member.Readiness, Is.EqualTo(SessionReadinessState.GameplayReady));

            var vitality = new VitalityRegistry();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(characterId, 100)), Is.True);
            DamageResult damage = vitality.ApplyDamage(new DamageRequest(characterId, 35));
            Assert.That(damage.Accepted, Is.True);
            Assert.That(damage.State.Current, Is.EqualTo(65));

            string[] testReferences = typeof(AuthoritativeGameplayWithoutHudTests).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            string[] sessionReferences = typeof(PartySession).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            string[] vitalityReferences = typeof(VitalityRegistry).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(testReferences.Any(IsHudAssembly), Is.False, "Headless regression test must not reference Hud assemblies.");
            Assert.That(sessionReferences.Any(IsHudAssembly), Is.False, "Sessions runtime must remain independent of Hud.");
            Assert.That(vitalityReferences.Any(IsHudAssembly), Is.False, "Vitality runtime must remain independent of Hud.");
        }

        private static bool IsHudAssembly(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("Game.Hud", StringComparison.Ordinal);
    }
}
