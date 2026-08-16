using Game.Structures.Runtime;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using GameCastlePlan = Game.Structures.Api.CastlePlan;
using GameCastleLayout = Game.Structures.Api.CastleLayout;
using EngineStructuresComposition = VoxelEngine.Composition.StructuresComposition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Game-composition castle plan wrapper retained only so the existing showcase world can be
    /// cut over without a 100K-line-risk rewrite. The authoritative definition is
    /// <see cref="Game.Structures.Api.CastlePlan"/>; this wrapper can disappear when the showcase
    /// world is split into smaller application-composition components.
    /// </summary>
    public struct CastlePlan
    {
        internal GameCastlePlan Value;

        public int3 Centre { get => Value.Centre; set => Value.Centre = value; }
        public int PlateauRadius { get => Value.PlateauRadius; set => Value.PlateauRadius = value; }
        public int PlateauHeight { get => Value.PlateauHeight; set => Value.PlateauHeight = value; }
        public int CliffDrop { get => Value.CliffDrop; set => Value.CliffDrop = value; }
        public int BaileyHalfX { get => Value.BaileyHalfX; set => Value.BaileyHalfX = value; }
        public int BaileyHalfZ { get => Value.BaileyHalfZ; set => Value.BaileyHalfZ = value; }
        public int WallHeight { get => Value.WallHeight; set => Value.WallHeight = value; }
        public int WallThickness { get => Value.WallThickness; set => Value.WallThickness = value; }
        public int TowerRadius { get => Value.TowerRadius; set => Value.TowerRadius = value; }
        public int TowerHeight { get => Value.TowerHeight; set => Value.TowerHeight = value; }
        public int GateTowerRadius { get => Value.GateTowerRadius; set => Value.GateTowerRadius = value; }
        public int GateTowerHeight { get => Value.GateTowerHeight; set => Value.GateTowerHeight = value; }
        public int KeepHalfX { get => Value.KeepHalfX; set => Value.KeepHalfX = value; }
        public int KeepHalfZ { get => Value.KeepHalfZ; set => Value.KeepHalfZ = value; }
        public int KeepHeight { get => Value.KeepHeight; set => Value.KeepHeight = value; }
        public int FloorHeight { get => Value.FloorHeight; set => Value.FloorHeight = value; }
        public int Floors { get => Value.Floors; set => Value.Floors = value; }
        public uint Seed { get => Value.Seed; set => Value.Seed = value; }

        public static implicit operator CastlePlan(GameCastlePlan plan) =>
            new() { Value = plan };

        public static implicit operator GameCastlePlan(CastlePlan plan) => plan.Value;
    }

    /// <summary>Showcase-local forwarding surface for game-owned castle geometry helpers.</summary>
    public static class CastleLayout
    {
        public const int TrapdoorHalfSize = GameCastleLayout.TrapdoorHalfSize;
        public const int ChapelBellTowerSize = GameCastleLayout.ChapelBellTowerSize;
        public const int ChapelBellTowerStairRadius = GameCastleLayout.ChapelBellTowerStairRadius;
        public const int FrontGateWidth = GameCastleLayout.FrontGateWidth;
        public const int FrontGateHeight = GameCastleLayout.FrontGateHeight;
        public const int FrontGateDepth = GameCastleLayout.FrontGateDepth;
        public const int LowerRiverDepth = GameCastleLayout.LowerRiverDepth;

        public static int3 TrapdoorCentre(in CastlePlan plan)
        {
            GameCastlePlan gamePlan = plan.Value;
            return GameCastleLayout.TrapdoorCentre(in gamePlan);
        }

        public static int3 FrontGateMinimum(in CastlePlan plan)
        {
            GameCastlePlan gamePlan = plan.Value;
            return GameCastleLayout.FrontGateMinimum(in gamePlan);
        }

        public static int WaterfallStreamX(in CastlePlan plan)
        {
            GameCastlePlan gamePlan = plan.Value;
            return GameCastleLayout.WaterfallStreamX(in gamePlan);
        }

        public static int LowerRiverZAt(in CastlePlan plan, int x)
        {
            GameCastlePlan gamePlan = plan.Value;
            return GameCastleLayout.LowerRiverZAt(in gamePlan, x);
        }

        public static int WaterfallLipZ(in CastlePlan plan)
        {
            GameCastlePlan gamePlan = plan.Value;
            return GameCastleLayout.WaterfallLipZ(in gamePlan);
        }

        public static int3 ChapelBellTowerCentre(in CastlePlan plan)
        {
            GameCastlePlan gamePlan = plan.Value;
            return GameCastleLayout.ChapelBellTowerCentre(in gamePlan);
        }
    }

    /// <summary>Showcase-local incremental build contract backed by game-owned castle content.</summary>
    public interface ICastleBuildSession
    {
        bool IsComplete { get; }
        int StageNumber { get; }
        long TotalVoxelsWritten { get; }
        bool Step();
    }

    /// <summary>
    /// Application composition facade. Generic engine wiring delegates to VoxelEngine.Composition;
    /// castle planning and construction terminate here in Game.Structures.
    /// </summary>
    public static class StructuresComposition
    {
        public static CastlePlan PlanCastle(int3 centre, uint seed) =>
            CastlePlanner.Plan(centre, seed);

        public static VoxelEngine.Composition.ArchLookdevBuildResult BuildArchLookdev(
            IVoxelStorageRuntime storage,
            in VoxelEngine.Composition.ArchLookdevBuildRequest request) =>
            EngineStructuresComposition.BuildArchLookdev(storage, in request);

        public static VoxelEngine.Composition.IStructureProfileStore CreateProfileStore() =>
            EngineStructuresComposition.CreateProfileStore();

        public static IStructureAuthoringSession CreateAuthoringSession(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials) =>
            EngineStructuresComposition.CreateAuthoringSession(reads, mutations, materials);

        public static IStructureAuthoringSession CreateAuthoringSession(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            int writeBudget) =>
            EngineStructuresComposition.CreateAuthoringSession(
                reads, mutations, materials, writeBudget);

        public static ICastleBuildSession BeginCastleBuild(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials)
        {
            IStructureAuthoringSession authoring =
                EngineStructuresComposition.CreateAuthoringSession(
                    reads, mutations, materials);
            GameCastlePlan gamePlan = plan.Value;
            return new CastleBuildSession(
                new CastleAuthoringBuild(authoring, in gamePlan, terrainSeed));
        }

        public static VoxelEngine.Composition.ReferenceArchBuildResult BuildReferenceArch(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            ISurfaceStyleAuthoringCatalogue surfaces,
            ICoatingAuthoringCatalogue coatings,
            VoxelEngine.Composition.IStructureProfileStore profiles,
            int3 origin,
            byte stoneMaterial,
            ushort pierStyle,
            ushort ringStyle,
            byte coating) =>
            EngineStructuresComposition.BuildReferenceArch(
                reads,
                mutations,
                materials,
                surfaces,
                coatings,
                profiles,
                origin,
                stoneMaterial,
                pierStyle,
                ringStyle,
                coating);

        private sealed class CastleBuildSession : ICastleBuildSession
        {
            private readonly CastleAuthoringBuild _build;

            public CastleBuildSession(CastleAuthoringBuild build)
            {
                _build = build;
            }

            public bool IsComplete => _build.IsComplete;
            public int StageNumber => _build.StageNumber;
            public long TotalVoxelsWritten => _build.TotalVoxelsWritten;
            public bool Step() => _build.Step();
        }
    }
}
