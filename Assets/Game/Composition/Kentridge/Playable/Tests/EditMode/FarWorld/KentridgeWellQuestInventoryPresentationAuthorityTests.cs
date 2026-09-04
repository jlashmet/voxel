using System;
using System.Linq;
using System.Reflection;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeWellQuestInventoryPresentationAuthorityTests
    {
        [Test]
        public void Presentation_HasNoAutonomousRuntimeAuthorityOrInstaller()
        {
            Type presentation = typeof(KentridgeWellQuestInventoryPresentation);
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo[] fields = presentation.GetFields(Fields);
            Assert.That(
                fields.Any(field => field.FieldType.FullName ==
                    "Game.Composition.Kentridge.Runtime.KentridgeCampaignSession"),
                Is.False,
                "Presentation must not retain canonical session authority.");
            Assert.That(
                fields.Any(field => field.FieldType.FullName ==
                    "Game.Input.Api.IInputContextService" ||
                    field.FieldType.FullName == "Game.Input.Runtime.InputContextService"),
                Is.False,
                "Presentation must not create or own an input-context authority.");

            MethodInfo[] methods = presentation.GetMethods(Methods);
            Assert.That(
                methods.Any(method => method.GetCustomAttributes(
                    typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length != 0),
                Is.False,
                "Presentation must be composed explicitly rather than globally auto-installed.");
            Assert.That(
                methods.Any(method => method.Name == "BindLiveSessionIfReady"),
                Is.False,
                "Presentation must not discover or reflect private runtime state.");
            Assert.That(
                presentation.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null,
                "Presentation must not poll physical input or drive gameplay commands from Update.");
        }

        [Test]
        public void Presentation_ExposesExplicitReadModelBinding()
        {
            MethodInfo bind = typeof(KentridgeWellQuestInventoryPresentation).GetMethod(
                "BindReadModel",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(bind, Is.Not.Null,
                "Production composition requires an explicit read-model binding seam.");
            ParameterInfo[] parameters = bind.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(4));
            Assert.That(parameters[0].ParameterType.FullName, Is.EqualTo("Game.Inventory.Api.IInventoryQuery"));
            Assert.That(parameters[1].ParameterType.FullName, Is.EqualTo("Game.Inventory.Api.InventoryId"));
            Assert.That(parameters[2].ParameterType.IsGenericType, Is.True);
            Assert.That(parameters[2].ParameterType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Func<>)));
            Assert.That(parameters[2].ParameterType.GetGenericArguments()[0].FullName,
                Is.EqualTo("Game.Quests.Api.QuestSnapshot"));
            Assert.That(parameters[3].ParameterType, Is.EqualTo(typeof(Vector3)));
        }

        [Test]
        public void ForestEncounter_HasNoAutonomousSceneInstaller()
        {
            Type encounter = Type.GetType(
                "Game.Composition.Kentridge.Playable.KentridgeForestBanditEncounter, Game.Composition.Kentridge.Playable");
            Assert.That(encounter, Is.Not.Null,
                "Kentridge forest encounter type must be available to the production playable composition.");

            const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods = encounter.GetMethods(Methods);
            Assert.That(
                methods.Any(method => method.GetCustomAttributes(
                    typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length != 0),
                Is.False,
                "Forest encounter must be supplied by explicit production composition, not a global scene installer.");
            Assert.That(
                methods.Any(method => method.Name == "InstallIntoPlayableSlice"),
                Is.False,
                "Forest encounter must not keep a hidden scene-name bootstrap fallback.");
        }
    }
}
