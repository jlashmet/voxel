using System;
using System.Collections.Generic;
using Game.Composition.Materials;
using Game.Materials.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Focused WorldBuilder-owned player proof for the macro physical catalogue. It realizes bounded
    /// slices of the source-backed production catalogue into real voxel storage and views them through
    /// the production renderer; no validation-only geometry or renderer is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MacroPhysicalWorldValidation : MonoBehaviour
    {
        private const uint Seed = 0x4B454E54u;
        private const int ViewSeconds = 12;

        private readonly struct ViewTarget
        {
            public readonly string Id;
            public readonly Int2 CentreDm;

            public ViewTarget(string id, Int2 centreDm)
            {
                Id = id;
                CentreDm = centreDm;
            }
        }

        private IVoxelStorageRuntime _storage;
        private FeatureCatalogue _catalogue;
        private ViewTarget[] _targets;
        private float _started;
        private int _viewIndex = -1;

        private void Start()
        {
            gameObject.tag = "MainCamera";
            Camera cameraComponent = gameObject.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.05f;
            cameraComponent.farClipPlane = 1800f;
            cameraComponent.fieldOfView = 58f;

            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetVoxelLodEnabled(true);
            RenderingComposition.SetVoxelRingRadiusMetres(180f);
            RenderingComposition.SetVoxelDetailBandScale(0.8f);
            GameMaterialComposition.Install();

            _storage = VoxelEngineBootstrap.CreateStorage(256, 131072);
            RegisterProductionMaterialIds();

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);

            Require(physical.Settlements.Count == 6, "all six source-backed settlements were not planned");
            Require(physical.BuildingCount >= 16, "generic settlement blockouts were lost");
            Require(physical.GeographyConstrainedRouteCount >= 3, "geography-constrained routes were lost");
            Require(
                physical.TryGetSettlement(MountingForceTopDownWorldDefinition.Moordell, out TopDownWorldSettlementPlan moordell)
                && moordell.Buildings.Count >= 4,
                "Moordell lacks its reusable physical settlement blockout");
            Require(
                physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.RossdamLake, out TopDownWorldRegionPlan lake)
                && lake.Spec.Kind == TopDownWorldRegionKind.WaterBody,
                "Rossdam Lake is absent from the physical plan");
            Require(
                physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernPass, out TopDownWorldRegionPlan pass)
                && pass.Spec.Kind == TopDownWorldRegionKind.ValleyPass,
                "Southern Ridge pass is absent from the physical plan");

            _catalogue = TopDownWorldPhysicalVoxelCatalogue.Build(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings,
                Allocator.Persistent);

            _targets = new[]
            {
                new ViewTarget("moordell", moordell.CentreDm),
                new ViewTarget("rossdam-lake", lake.CentreDm),
                new ViewTarget("southern-ridge-pass", pass.CentreDm),
            };

            var realized = new HashSet<int3>();
            for (int i = 0; i < _targets.Length; i++)
                RealizeAround(_targets[i].CentreDm, realized);

            _storage.PublishAllResidentRegions();
            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(world, _storage.Changes, Seed, farFieldEnabled: false);
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _started = Time.unscaledTime;
            SetView(0);
            Debug.Log(
                "MACRO_PHYSICAL_WORLD ready: " +
                $"settlements={physical.Settlements.Count} buildings={physical.BuildingCount} " +
                $"regions={physical.Regions.Count} routes={physical.Routes.Count} " +
                $"constrainedRoutes={physical.GeographyConstrainedRouteCount} realizedRegions={realized.Count}");
        }

        private void Update()
        {
            if (_storage == null || _targets == null) return;
            int requested = Mathf.Min(_targets.Length - 1, (int)((Time.unscaledTime - _started) / ViewSeconds));
            if (requested != _viewIndex) SetView(requested);
            PositionCamera(_targets[_viewIndex].CentreDm);
        }

        private void SetView(int index)
        {
            _viewIndex = Mathf.Clamp(index, 0, _targets.Length - 1);
            PositionCamera(_targets[_viewIndex].CentreDm);
            Debug.Log("MACRO_PHYSICAL_WORLD view=" + _targets[_viewIndex].Id);
        }

        private void PositionCamera(Int2 centreDm)
        {
            float x = centreDm.X * 0.1f;
            float z = centreDm.Y * 0.1f;
            transform.position = new Vector3(x - 32f, 42f, z - 46f);
            Vector3 lookTarget = new Vector3(x, 23f, z);
            transform.rotation = Quaternion.LookRotation((lookTarget - transform.position).normalized, Vector3.up);
        }

        private void RealizeAround(Int2 centreDm, HashSet<int3> realized)
        {
            int voxelX = centreDm.X;
            int voxelZ = centreDm.Y;
            int ground = VoxelEngine.Terrain.Api.TerrainQuery.HeightAt(voxelX, voxelZ, Seed);
            int regionX = FloorRegion(voxelX);
            int regionY = FloorRegion(ground);
            int regionZ = FloorRegion(voxelZ);

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int3 region = new int3(regionX + dx, regionY + dy, regionZ + dz);
                if (!realized.Add(region)) continue;
                _storage.Residency.EnsureRegionResident(region);
                using var build = new FeatureRegionBuild(region);
                while (!build.Step(_catalogue, Seed, _storage.Reads, _storage.Mutations, int.MaxValue)) { }
                if (build.Report.BudgetExceeded)
                    throw new InvalidOperationException(
                        "MACRO_PHYSICAL_WORLD failure: production catalogue exceeded its authored work budget.");
            }
        }

        private static int FloorRegion(int voxel) =>
            (int)Math.Floor((double)voxel / VoxelGrid.RegionVoxelEdge);

        private void RegisterProductionMaterialIds()
        {
            _storage.RegisterMaterial(GameMaterialIds.Stone, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.MasonryMedium, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.DarkStone, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.Wood, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.Glass, 12, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.LitWindow, 12, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.Tile, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.Slate, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.Cloth, 12, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.Moss, 12, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.Water, 12, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(GameMaterialIds.MasonrySmall, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
        }

        private static VoxelWorldGenSettings Settings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: GameMaterialIds.Stone,
                masonry: GameMaterialIds.MasonryMedium,
                darkMasonry: GameMaterialIds.DarkStone,
                timber: GameMaterialIds.Wood,
                glass: GameMaterialIds.Glass,
                warmWindow: GameMaterialIds.LitWindow,
                roofTile: GameMaterialIds.Tile,
                slate: GameMaterialIds.Slate,
                cloth: GameMaterialIds.Cloth,
                moss: GameMaterialIds.Moss,
                water: GameMaterialIds.Water,
                roadSurface: GameMaterialIds.MasonrySmall);
            return new VoxelWorldGenSettings(1, materials);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("MACRO_PHYSICAL_WORLD failure: " + message);
        }

        private void OnDestroy()
        {
            RenderingComposition.ClearWorld();
            if (_catalogue.IsCreated) _catalogue.Dispose();
            _storage?.Dispose();
            _storage = null;
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.SetSurfaceBuildEnabled(true);
        }
    }
}
