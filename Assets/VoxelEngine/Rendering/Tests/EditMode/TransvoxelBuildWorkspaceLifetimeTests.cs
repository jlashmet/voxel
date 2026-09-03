using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class TransvoxelBuildWorkspaceLifetimeTests
    {
        [Test]
        public void NativeContainerOwnersAreMutable()
        {
            FieldInfo[] nativeOwners = typeof(TransvoxelBuildWorkspace)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field =>
                {
                    Type type = field.FieldType;
                    if (!type.IsGenericType) return false;
                    Type definition = type.GetGenericTypeDefinition();
                    return definition == typeof(NativeArray<>) || definition == typeof(NativeList<>);
                })
                .ToArray();

            Assert.That(nativeOwners, Is.Not.Empty);
            Assert.That(nativeOwners.Where(field => field.IsInitOnly).Select(field => field.Name),
                Is.Empty,
                "NativeContainer lifecycle owners must be mutable so Dispose clears their handles.");
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var workspace = new TransvoxelBuildWorkspace(
                gridSampleCount: 8,
                brickCacheCount: 1,
                samplesFromMips: false,
                usesBlockHlod: false,
                supportsFeaturePreservingFallback: false,
                hlodCoreBrickEdge: 1,
                cellsPerAxis: 1,
                faceSamplesPerAxis: 2);

            workspace.Dispose();
            Assert.DoesNotThrow(workspace.Dispose);
        }
    }
}
