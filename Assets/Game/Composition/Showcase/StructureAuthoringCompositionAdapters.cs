using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using GameCastlePlan = Game.Structures.Api.CastlePlan;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase bootstrap wiring for Game.Structures API capabilities. Concrete VoxelEngine
    /// implementations are intentionally constructed here at the Composition boundary.
    /// </summary>
    internal static class ShedAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in ShedConfig config) =>
            Game.Structures.Runtime.ShedAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);
    }

    internal static class ChurchAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in ChurchConfig config) =>
            Game.Structures.Runtime.ChurchAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);
    }

    internal static class CathedralAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in CathedralConfig config) =>
            Game.Structures.Runtime.CathedralAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);
    }

    internal static class CathedralWorldbuildingAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin,
            in CathedralWorldbuildingConfig config) =>
            Game.Structures.Runtime.CathedralWorldbuildingAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);
    }

    internal static class TempleAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in TempleConfig config) =>
            Game.Structures.Runtime.TempleAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);
    }

    /// <summary>
    /// Composition-owned castle build facade that supplies the concrete cave runtime while keeping
    /// Game.Structures.Runtime dependent only on the API capability.
    /// </summary>
    internal sealed class CastleAuthoringBuild
    {
        private readonly Game.Structures.Runtime.CastleAuthoringBuild _inner;

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in GameCastlePlan plan, uint terrainSeed)
        {
            _inner = new Game.Structures.Runtime.CastleAuthoringBuild(
                authoring, new CaveAuthoringService(), in plan, terrainSeed);
        }

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in GameCastlePlan plan,
            CastleComponentConfig components, uint terrainSeed)
        {
            _inner = new Game.Structures.Runtime.CastleAuthoringBuild(
                authoring, new CaveAuthoringService(), in plan, components, terrainSeed);
        }

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in GameCastlePlan plan,
            CastleComponentConfig components, CastleCurtainConfig curtain, uint terrainSeed)
        {
            _inner = new Game.Structures.Runtime.CastleAuthoringBuild(
                authoring, new CaveAuthoringService(), in plan, components, curtain, terrainSeed);
        }

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in GameCastlePlan plan,
            CastlePresetConfig preset, uint terrainSeed)
        {
            _inner = new Game.Structures.Runtime.CastleAuthoringBuild(
                authoring, new CaveAuthoringService(), in plan, preset, terrainSeed);
        }

        public bool IsComplete => _inner.IsComplete;
        public int StageNumber => _inner.StageNumber;
        public long TotalVoxelsWritten => _inner.TotalVoxelsWritten;
        public bool Step() => _inner.Step();
    }
}
