using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class CpuWaterSurfaceChunkCacheLifetimeTests
    {
        [TestCase("_brickMaterials")]
        [TestCase("_surfaceScratch")]
        [TestCase("_boundaryScratch")]
        public void PersistentNativeArrayOwnerMustRemainMutable(string fieldName)
        {
            FieldInfo field = typeof(CpuWaterSurfaceChunkCache).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Missing lifetime field {fieldName}.");
            Assert.That(field.FieldType.IsGenericType, Is.True);
            Assert.That(field.FieldType.GetGenericTypeDefinition(), Is.EqualTo(typeof(NativeArray<>)));
            Assert.That(field.IsInitOnly, Is.False,
                $"{fieldName} must not be readonly: NativeArray.Dispose mutates the owner struct to clear its native handle.");
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var cache = new CpuWaterSurfaceChunkCache();
            Assert.DoesNotThrow(cache.Dispose);
            Assert.DoesNotThrow(cache.Dispose);
        }
    }
}
