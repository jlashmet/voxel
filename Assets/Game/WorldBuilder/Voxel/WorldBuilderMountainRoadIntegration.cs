using System;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Composes an authored mountain surface over another road-terrain authority without changing
    /// either source. Horizontal occupancy is taken from the exact analytic mass footprints, while
    /// height remains the same MountainLandformSurface query consumed by voxel realization.
    /// </summary>
    public sealed class MountainLandformRoadTerrain : IWorldRoadTerrain
    {
        private readonly MountainLandformSurface _mountain;
        private readonly IWorldRoadTerrain _fallback;

        public MountainLandformRoadTerrain(
            MountainLandformSurface mountain,
            IWorldRoadTerrain fallback)
        {
            _mountain = mountain ?? throw new ArgumentNullException(nameof(mountain));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public int HeightAtDm(int xdm, int zdm)
        {
            int fallbackHeight = _fallback.HeightAtDm(xdm, zdm);
            if (!ContainsMountainColumn(xdm, zdm)) return fallbackHeight;
            return Math.Max(fallbackHeight, _mountain.HeightAtDm(xdm, zdm));
        }

        public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm)
        {
            WorldRoadTerrainFlags flags = _fallback.FlagsAtDm(xdm, zdm);
            if (ContainsMountainColumn(xdm, zdm))
                flags |= _mountain.FlagsAtDm(xdm, zdm);
            return flags;
        }

        private bool ContainsMountainColumn(int xdm, int zdm)
        {
            for (int i = 0; i < _mountain.MassCount; i++)
            {
                MountainLandformMass mass = _mountain.GetMass(i);
                long dx = (long)xdm - mass.CentreXdm;
                long dz = (long)zdm - mass.CentreZdm;
                if (dx * dx + dz * dz <= (long)mass.BaseRadiusDm * mass.BaseRadiusDm)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Game-facing lowering boundary for the shared WorldRoadNetwork voxel catalogue. Composition
    /// supplies only the semantic network and its concrete road-surface material; backend settings
    /// stay behind Game.WorldBuilder.Voxel just like the authored-town adapter.
    /// </summary>
    public static class WorldBuilderRoadVoxelCatalogue
    {
        public static FeatureCatalogue Build(
            WorldRoadNetwork network,
            byte roadSurfaceMaterial,
            Allocator allocator,
            int precedence = 110)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (roadSurfaceMaterial == 0) throw new ArgumentOutOfRangeException(nameof(roadSurfaceMaterial));

            var backendMaterials = new VoxelMaterialMap(
                foundationStone: roadSurfaceMaterial,
                masonry: roadSurfaceMaterial,
                darkMasonry: roadSurfaceMaterial,
                timber: roadSurfaceMaterial,
                glass: roadSurfaceMaterial,
                warmWindow: roadSurfaceMaterial,
                roofTile: roadSurfaceMaterial,
                slate: roadSurfaceMaterial,
                cloth: roadSurfaceMaterial,
                moss: roadSurfaceMaterial,
                water: roadSurfaceMaterial,
                roadSurface: roadSurfaceMaterial);
            var settings = new VoxelWorldGenSettings(
                voxelsPerDecimetre: 1,
                materials: backendMaterials);
            return WorldRoadNetworkVoxelCatalogue.Build(
                network,
                settings,
                allocator,
                precedence);
        }
    }

    /// <summary>
    /// Keeps the temporary summit marker independent from mountain shape and traversal ownership.
    /// Its position is derived from the physical core mass, so a composition can replace the marker
    /// without changing the reusable landform or road contracts.
    /// </summary>
    public static class WorldBuilderMountainSummitPlaceholderCatalogue
    {
        public const string DefinitionName = "worldbuilder-mountain-summit-placeholder";

        public static FeatureCatalogue Build(
            MountainLandformSurface surface,
            int sizeDm,
            byte material,
            Allocator allocator,
            int precedence = 120)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (sizeDm < 1) throw new ArgumentOutOfRangeException(nameof(sizeDm));
            if (material == 0) throw new ArgumentOutOfRangeException(nameof(material));

            MountainLandformMass summit = surface.GetMass(0);
            if (sizeDm > summit.TopRadiusDm * 2)
                throw new ArgumentOutOfRangeException(
                    nameof(sizeDm),
                    "Summit placeholder must fit on the authoritative core crest.");

            int programLength = ShapeOps.InstructionLength(ShapeOp.EmitBox)
                + ShapeOps.InstructionLength(ShapeOp.End);
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            int pc = 0;
            catalogue.Program[pc++] = (int)ShapeOp.EmitBox;
            catalogue.Program[pc++] = 0;
            catalogue.Program[pc++] = 0;
            catalogue.Program[pc++] = 0;
            catalogue.Program[pc++] = 0;
            catalogue.Program[pc++] = sizeDm;
            catalogue.Program[pc++] = sizeDm;
            catalogue.Program[pc++] = sizeDm;
            catalogue.Program[pc++] = material;
            catalogue.Program[pc++] = 0;
            catalogue.Program[pc++] = 0;
            catalogue.Program[pc++] = (int)PrimitiveMode.Fill;
            catalogue.Program[pc++] = (int)ShapeOp.End;
            catalogue.Program[pc] = 0;

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = DefinitionName,
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = summit.TopYdm + 1,
                Footprint = new int3(sizeDm, sizeDm, sizeDm),
                MaxSlope = 0,
                Precedence = precedence,
                ProgramOffset = 0,
                ProgramLength = programLength,
                MaxPrimitives = 1,
            };

            int half = sizeDm / 2;
            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = new int3(
                    summit.CentreXdm - half,
                    summit.TopYdm + 1,
                    summit.CentreZdm - half),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = 0,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 4096,
                MaxSlope = 0,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result == CatalogueLoadResult.Ok) return catalogue;
            catalogue.Dispose();
            throw new InvalidOperationException(
                "Mountain summit placeholder catalogue failed validation: " + result);
        }
    }
}
