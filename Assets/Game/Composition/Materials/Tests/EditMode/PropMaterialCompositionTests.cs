using System.Collections.Generic;
using Game.Materials.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Composition.Materials.Tests
{
    /// <summary>
    /// Exercises the real shared adapter, not a second material registry or fake renderer.
    /// The module-owned standalone scene supplies the corresponding pixel evidence.
    /// </summary>
    [NonParallelizable]
    public sealed class PropMaterialCompositionTests
    {
        [Test]
        public void RegisteredMaterials_PreserveDefinitionResponseAndReuseSharedInstances()
        {
            var definitions = GameMaterialRenderingDefinitions.Create();
            Assert.That(definitions, Is.Not.Empty);
            var ids = new HashSet<int>();
            foreach (var definition in definitions)
            {
                Assert.That(ids.Add(definition.MaterialIndex), Is.True, "Duplicate canonical material index.");
                Assert.That(definition.MaterialIndex, Is.InRange(0, (int)byte.MaxValue));
                byte id = (byte)definition.MaterialIndex;
                Assert.That(GameMaterialComposition.TryGetProceduralMaterial(id, out Material first), Is.True,
                    $"Canonical material {id} is not presentable.");
                Assert.That(first, Is.Not.Null);
                Assert.That(first.shader, Is.Not.Null);
                Assert.That(GameMaterialComposition.TryGetProceduralMaterial(id, out Material second), Is.True);
                Assert.That(second, Is.SameAs(first), "Resolving a prop must not allocate a second shared material.");
                Assert.That(first.color.r, Is.EqualTo(definition.Albedo.x).Within(0.000001f));
                Assert.That(first.color.g, Is.EqualTo(definition.Albedo.y).Within(0.000001f));
                Assert.That(first.color.b, Is.EqualTo(definition.Albedo.z).Within(0.000001f));
                Assert.That(first.color.a, Is.EqualTo(definition.Albedo.w).Within(0.000001f));
                Assert.That(first.HasProperty("_Smoothness"), Is.True);
                Assert.That(first.GetFloat("_Smoothness"),
                    Is.EqualTo(Mathf.Clamp01(1f - definition.Surface.z)).Within(0.000001f));
            }
        }

        [Test]
        public void UnknownMaterials_FailExplicitlyWithoutInventingFallbackPresentation()
        {
            var ids = new HashSet<int>();
            foreach (var definition in GameMaterialRenderingDefinitions.Create())
                ids.Add(definition.MaterialIndex);
            int checkedUnknown = 0;
            for (int id = 0; id <= byte.MaxValue; id++)
            {
                if (ids.Contains(id)) continue;
                Assert.That(GameMaterialComposition.TryGetProceduralMaterial((byte)id, out Material material), Is.False);
                Assert.That(material, Is.Null, $"Unknown material {id} acquired a substitute presentation.");
                checkedUnknown++;
            }
            Assert.That(checkedUnknown, Is.GreaterThan(0));
        }
    }
}
