using System;
using System.Reflection;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldSettlementSurveyExecutionOrderTests
    {
        [Test]
        public void SettlementLensRunsAfterEvidenceDriver()
        {
            Type compositionType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldSettlementSurveyComposition", true);
            Type driverType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldEvidenceDriver", true);

            DefaultExecutionOrder compositionOrder = compositionType.GetCustomAttribute<DefaultExecutionOrder>();
            DefaultExecutionOrder driverOrder = driverType.GetCustomAttribute<DefaultExecutionOrder>();
            int effectiveDriverOrder = driverOrder == null ? 0 : driverOrder.order;

            Assert.That(compositionOrder, Is.Not.Null);
            Assert.That(compositionOrder.order, Is.GreaterThan(effectiveDriverOrder));
        }
    }
}
