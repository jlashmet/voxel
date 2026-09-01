using System;
using Game.Structures.Runtime;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using EnginePresentationComposition = VoxelEngine.Composition.StructurePresentationComposition;
using GameCastlePlan = Game.Structures.Api.CastlePlan;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-owned policy for feeding normal generated structure authoring into the engine's
    /// generic nonresident presentation capture. No castle vocabulary crosses the shared API.
    /// </summary>
    internal static class ShowcaseStructurePresentation
    {
        private const ulong CastleSourceDomain = 0x434153544C455052ul; // "CASTLEPR"

        public static FeaturePresentationBake BakeCastle(
            in CastlePlan plan,
            uint worldSeed,
            Func<int, int, int, byte> baselineMaterial)
        {
            IStructurePresentationCaptureSession capture =
                EnginePresentationComposition.CreateCaptureSession(baselineMaterial);
            GameCastlePlan gamePlan = plan.Value;
            var build = new CastleAuthoringBuild(capture, in gamePlan, worldSeed);
            while (!build.Step()) { }

            ulong sourceId = Mix(CastleSourceDomain
                                 ^ unchecked((uint)plan.Centre.x)
                                 ^ ((ulong)unchecked((uint)plan.Centre.y) << 21)
                                 ^ ((ulong)unchecked((uint)plan.Centre.z) << 42));
            ulong revisionSeed = Mix(CastleSourceDomain ^ worldSeed ^ plan.Seed);
            return capture.Bake(
                sourceId,
                revisionSeed,
                FeatureKind.Structure,
                plan.Centre,
                0);
        }

        private static ulong Mix(ulong value)
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9ul;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBul;
            return value ^ (value >> 31);
        }
    }
}
