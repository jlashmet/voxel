using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Replays the canonical feature shape program into its ordered primitive stream without
    /// rasterising a voxel region. The result is derived presentation data only; catalogue,
    /// placement, and seed remain authoritative world-generation inputs.
    /// </summary>
    public sealed class FeaturePresentationBaker : IFeaturePresentationBaker
    {
        public bool TryBake(
            in FeatureCatalogue catalogue,
            uint worldSeed,
            int definitionId,
            in ExplicitPlacement placement,
            out FeaturePresentationBake bake)
        {
            bake = null;
            if (!catalogue.IsCreated
                || (uint)definitionId >= (uint)catalogue.DefinitionCount)
                return false;

            FeatureDefinition definition = catalogue.Definitions[definitionId];
            using var primitives = new NativeList<Primitive>(Allocator.Temp);
            using var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);

            EvaluationResult evaluation = FeatureGeneration.EvaluateInstance(
                in catalogue,
                worldSeed,
                definitionId,
                in definition,
                in placement,
                primitives,
                anchors);
            if (evaluation != EvaluationResult.Ok || primitives.Length == 0)
                return false;

            Primitive first = primitives[0];
            first.Bounds(out int3 boundsMin, out int3 boundsMax);
            for (int i = 1; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                primitive.Bounds(out int3 primitiveMin, out int3 primitiveMax);
                boundsMin = math.min(boundsMin, primitiveMin);
                boundsMax = math.max(boundsMax, primitiveMax);
            }

            var copied = new Primitive[primitives.Length];
            ulong revision = FeatureHash.Mix(
                catalogue.Hash
                ^ FeatureHash.Cell(worldSeed, definitionId, placement.Position)
                ^ placement.Orientation);
            for (int i = 0; i < primitives.Length; i++)
            {
                copied[i] = primitives[i];
                revision = HashPrimitive(revision, in copied[i]);
            }

            ulong sourceId = FeatureHash.Cell(worldSeed, definitionId, placement.Position);
            bake = new FeaturePresentationBake(
                sourceId,
                revision,
                definition.Kind,
                placement.Position,
                placement.Orientation,
                boundsMin,
                boundsMax,
                copied);
            return true;
        }

        private static ulong HashPrimitive(ulong hash, in Primitive primitive)
        {
            hash = Hash(hash, (ulong)primitive.Shape);
            hash = Hash(hash, (ulong)primitive.Mode);
            hash = Hash(hash, primitive.Material);
            hash = Hash(hash, primitive.SurfaceStyle);
            hash = Hash(hash, primitive.Coating);
            hash = Hash(hash, (ulong)primitive.SurfaceFlags);
            hash = Hash(hash, primitive.SurfaceDetail);
            hash = Hash(hash, primitive.Axis);
            hash = Hash(hash, unchecked((byte)primitive.Direction));
            hash = Hash(hash, (ulong)primitive.Profile);
            hash = Hash(hash, unchecked((uint)primitive.Order));
            hash = Hash(hash, unchecked((uint)primitive.A.x));
            hash = Hash(hash, unchecked((uint)primitive.A.y));
            hash = Hash(hash, unchecked((uint)primitive.A.z));
            hash = Hash(hash, unchecked((uint)primitive.B.x));
            hash = Hash(hash, unchecked((uint)primitive.B.y));
            hash = Hash(hash, unchecked((uint)primitive.B.z));
            hash = Hash(hash, unchecked((uint)primitive.Radius));
            hash = Hash(hash, unchecked((uint)primitive.InnerRadius));
            hash = Hash(hash, unchecked((uint)primitive.C.x));
            hash = Hash(hash, unchecked((uint)primitive.C.y));
            hash = Hash(hash, unchecked((uint)primitive.C.z));
            hash = Hash(hash, unchecked((uint)primitive.D.x));
            hash = Hash(hash, unchecked((uint)primitive.D.y));
            hash = Hash(hash, unchecked((uint)primitive.D.z));
            hash = Hash(hash, unchecked((uint)primitive.StartDirection.x));
            hash = Hash(hash, unchecked((uint)primitive.StartDirection.y));
            hash = Hash(hash, unchecked((uint)primitive.EndDirection.x));
            hash = Hash(hash, unchecked((uint)primitive.EndDirection.y));
            return hash;
        }

        private static ulong Hash(ulong hash, ulong value) =>
            FeatureHash.Mix(hash ^ FeatureHash.Mix(value + 0x9E3779B97F4A7C15ul));
    }
}
