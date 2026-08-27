using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private const int BakeMaxRegionSnapshotBytes = ShowcaseWorldBakeCodec.MaxRawRegionPayloadBytes;
        private const int BakeHeightPipelineDepth = 4;
        private static readonly TimeSpan BakeCastleTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Produces the finished startup world synchronously for an editor/build-time baker.
        /// Nothing in gameplay calls this path. Castle authoring and nearby terrain/features are
        /// paid once, then captured as semantic storage snapshots for later sessions.
        /// </summary>
        public void GenerateForBakeBlocking(int startupRadiusRegions)
        {
            EnsureFreshForBake();
            int radius = math.clamp(startupRadiusRegions, 0, LoadRadiusRegions);

            // Queue the deterministic landmark footprint, then materialise every terrain region
            // it can touch before authoring. This preserves the same ordering requirements as
            // runtime generation without making a player watch those stages execute.
            GenerateCastleOriginForBakeBlocking();
            for (int i = 0; i < _castleRegions.Count; i++)
                GenerateRegionBlocking(_castleRegions[i]);

            // Castle authoring uses a private store until publication, so let that worker overlap
            // the remaining, disjoint startup terrain. Castle-owned terrain is already resident
            // and MaterialiseStartupDisc de-duplicates it; publication still happens afterwards
            // on the main thread, while sorted bake capture preserves authoritative output order.
            StartCastleDuringBake();
            MaterialiseStartupDisc(RegionAt(SpawnPosition()), radius);
            WaitForCastleDuringBake();

            if (_pendingFeatureRegions.Count != 0 || _featureBuild != null)
                throw new InvalidOperationException(
                    "Offline showcase bake ended with unfinished generic feature work.");
        }

        /// <summary>
        /// Produces the finished worldbuilding gallery world for the offline baker.
        ///
        /// The gallery is the more expensive of the two showcases to start: it pays the castle,
        /// then a 48-million-voxel authoring pass over seven exhibits, a promenade, a cave walk,
        /// and two furnished guild houses — every time the scene is entered. None of that is
        /// per-session state, so all of it belongs in an image.
        /// </summary>
        public void GenerateGalleryForBakeBlocking(int startupRadiusRegions)
        {
            EnsureFreshForBake();
            int radius = math.clamp(startupRadiusRegions, 0, LoadRadiusRegions);

            // The castle is authored on a worker and is part of this world too, so it has to be
            // complete before the gallery writes anything: a castle region finishing later would
            // overwrite gallery voxels wholesale, which is the same failure terrain generation
            // causes when it runs after a landmark rather than before it.
            GenerateCastleOriginForBakeBlocking();
            for (int i = 0; i < _castleRegions.Count; i++)
                GenerateRegionBlocking(_castleRegions[i]);
            WaitForCastleDuringBake();

            GenerateWorldbuildingGalleryBlocking(null);
            GenerateWorldbuildingGalleryTourExpansionBlocking();

            // Fill the whole streamed radius around the gallery spawn, not a smaller startup disc.
            // The original showcase learned this the hard way: a bake smaller than the streamed
            // radius leaves a gap the budget-sliced streamer produces at a few seconds of work per
            // real minute, so the ground simply never arrives while the LOD rings keep asking for
            // it. Baking the whole radius removes the handover rather than tuning it.
            MaterialiseStartupDisc(RegionAt(WorldbuildingGallerySpawnPosition()), radius);

            if (_pendingFeatureRegions.Count != 0 || _featureBuild != null)
                throw new InvalidOperationException(
                    "Offline gallery bake ended with unfinished generic feature work.");
        }

        private void WaitForCastleDuringBake()
        {
            DateTime castleDeadline = DateTime.UtcNow + BakeCastleTimeout;
            while (!_hasCastlePlan)
            {
                if (!StepLandmarks())
                    throw new InvalidOperationException(
                        "Showcase castle could not advance during offline baking.");
                if (DateTime.UtcNow >= castleDeadline)
                    throw new TimeoutException(
                        $"Showcase castle did not finish within {BakeCastleTimeout.TotalMinutes:F0} minutes during offline baking.");

                if (_castleBuild != null && _castleBuild.StageNumber == 2)
                    Thread.Sleep(1);
            }
        }

        private void StartCastleDuringBake()
        {
            if (_hasCastlePlan) return;
            if (!StepLandmarks() || _castleBuild == null)
                throw new InvalidOperationException(
                    "Showcase castle worker could not start during offline baking.");
        }

        private void MaterialiseStartupDisc(int3 centre, int radius)
        {
            var orderedRegions = new List<int3>();
            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                if (dx * dx + dz * dz > radius * radius) continue;

                int rx = centre.x + dx;
                int rz = centre.z + dz;
                SurfaceLayerSpan(rx, rz, out int minLayer, out int maxLayer);
                if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                    maxLayer = minLayer + MaxSurfaceLayersPerColumn;

                for (int ry = minLayer; ry <= maxLayer; ry++)
                    orderedRegions.Add(new int3(rx, ry, rz));

                if (centre.y < minLayer || centre.y > maxLayer)
                    orderedRegions.Add(new int3(rx, centre.y, rz));
            }

            GenerateTerrainRegionsForBakeBlocking(orderedRegions, BakeHeightPipelineDepth);
        }

        /// <summary>
        /// Overlaps the independent Burst height jobs for a small bounded group of regions, then
        /// performs all storage and feature mutation in the original deterministic order.
        /// </summary>
        internal void GenerateTerrainRegionsForBakeBlocking(
            IReadOnlyList<int3> orderedRegions,
            int pipelineDepth)
        {
            if (orderedRegions == null) throw new ArgumentNullException(nameof(orderedRegions));
            if (pipelineDepth < 1 || pipelineDepth > 16)
                throw new ArgumentOutOfRangeException(nameof(pipelineDepth));

            var pending = new List<int3>(orderedRegions.Count);
            var seen = new HashSet<int3>();
            for (int i = 0; i < orderedRegions.Count; i++)
            {
                int3 coord = orderedRegions[i];
                if (_generated.Contains(coord) || !seen.Add(coord)) continue;
                pending.Add(coord);
            }

            for (int start = 0; start < pending.Count; start += pipelineDepth)
            {
                int count = math.min(pipelineDepth, pending.Count - start);
                var prepared = new PreparedBakeRegionHeight[count];
                try
                {
                    for (int i = 0; i < count; i++)
                        prepared[i] = PrepareBakeRegionHeight(pending[start + i]);
                    JobHandle.ScheduleBatchedJobs();

                    for (int i = 0; i < count; i++)
                        GeneratePreparedRegionBlocking(prepared[i]);
                }
                finally
                {
                    for (int i = 0; i < count; i++)
                        prepared[i]?.Dispose();
                }
            }
        }

        private PreparedBakeRegionHeight PrepareBakeRegionHeight(int3 coord)
        {
            var heights =
                new NativeArray<int>(RegionVoxelEdge * RegionVoxelEdge, Allocator.Persistent);
            int3 originVoxel = coord * RegionVoxelEdge;
            JobHandle job = new ShowcaseHeightJob
            {
                Heights = heights,
                Origin = new int2(originVoxel.x, originVoxel.z),
                Edge = RegionVoxelEdge,
                Seed = Seed,
            }.Schedule(heights.Length, 256);
            return new PreparedBakeRegionHeight(coord, heights, job);
        }

        private void GeneratePreparedRegionBlocking(PreparedBakeRegionHeight prepared)
        {
            if (_gen.Active) FinishRegionForced();
            BeginRegion(prepared.Coord, prepared.Heights, prepared.HeightJob);
            prepared.TransferOwnership();
            try
            {
                CompleteRegionBlocking(prepared.Coord);
            }
            catch
            {
                FinishRegionForced();
                throw;
            }
        }

        private sealed class PreparedBakeRegionHeight : IDisposable
        {
            private bool _transferred;

            public PreparedBakeRegionHeight(
                int3 coord,
                NativeArray<int> heights,
                JobHandle heightJob)
            {
                Coord = coord;
                Heights = heights;
                HeightJob = heightJob;
            }

            public int3 Coord { get; }
            public NativeArray<int> Heights { get; }
            public JobHandle HeightJob { get; }

            public void TransferOwnership()
            {
                _transferred = true;
            }

            public void Dispose()
            {
                if (_transferred) return;
                HeightJob.Complete();
                if (Heights.IsCreated) Heights.Dispose();
            }
        }

        /// <summary>Captures every resident region produced by <see cref="GenerateForBakeBlocking"/>.</summary>
        public ShowcaseWorldBake CaptureBake(int startupRadiusRegions)
        {
            if (!_hasCastlePlan || CastleVoxels <= 0)
                throw new InvalidOperationException(
                    "The showcase castle must be complete before a startup bake can be captured.");
            if (_gen.Active || _pendingFeatureRegions.Count != 0 || _featureBuild != null)
                throw new InvalidOperationException(
                    "The showcase world still has generation work in flight and is not bake-stable.");

            // Castle publication and authored features are allowed to create resident regions that
            // were not terrain-generation queue entries. Capturing only _generated would silently
            // omit those authored regions from the startup image. Storage residency is the source
            // of truth for the finished world image.
            NativeArray<int3> resident = _table.GetResidentCoords(Allocator.Temp);
            var coords = new List<int3>(resident.Length);
            try
            {
                for (int i = 0; i < resident.Length; i++)
                    coords.Add(resident[i]);
            }
            finally
            {
                resident.Dispose();
            }

            coords.Sort(CompareRegionCoords);
            var regions = new List<ShowcaseWorldBakedRegion>(coords.Count);
            IRegionSnapshotSource source = SnapshotStorage;

            for (int i = 0; i < coords.Count; i++)
            {
                int3 coord = coords[i];
                RegionSnapshotCaptureResult result = source.CaptureSemanticSnapshot(
                    coord, BakeMaxRegionSnapshotBytes, out RegionSemanticSnapshot snapshot);
                if (result != RegionSnapshotCaptureResult.Ok)
                    throw new InvalidDataException(
                        $"Could not capture baked region {coord}: {result}.");
                regions.Add(new ShowcaseWorldBakedRegion(
                    coord, snapshot.SemanticHash, snapshot.Bytes));
            }

            return new ShowcaseWorldBake(
                Seed,
                math.clamp(startupRadiusRegions, 0, LoadRadiusRegions),
                CastleVoxels,
                FeatureVoxelsBuilt,
                FeatureInstancesBuilt,
                ReferenceArchMin,
                ReferenceArchMax,
                regions,
                HasGalleryContent,
                GalleryCavePathEnd);
        }

        /// <summary>
        /// Restores a previously baked startup world into a fresh ShowcaseWorld. This is the only
        /// startup path gameplay needs: no terrain generator or castle authoring session runs
        /// before the player is spawned.
        /// </summary>
        public void LoadBake(ShowcaseWorldBake bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            EnsureFreshForBake();
            if (bake.Seed != Seed)
                throw new InvalidDataException(
                    $"Showcase bake seed 0x{bake.Seed:X8} does not match scene seed 0x{Seed:X8}. Re-bake the world.");
            if (bake.StartupRadiusRegions > LoadRadiusRegions)
                throw new InvalidDataException(
                    $"Showcase bake startup radius {bake.StartupRadiusRegions} exceeds runtime load radius {LoadRadiusRegions}.");

            var seen = new HashSet<int3>();
            for (int i = 0; i < bake.Regions.Count; i++)
            {
                ShowcaseWorldBakedRegion region = bake.Regions[i];
                if (!seen.Add(region.Coord))
                    throw new InvalidDataException(
                        $"Showcase bake contains duplicate region {region.Coord}.");

                // Keep the on-disk image compressed. Inflate only the region currently being
                // installed, verify it through Storage.Api, then let that temporary byte array go
                // before moving to the next region. Runtime never materialises the raw whole world.
                byte[] semanticPayload = ShowcaseWorldBakeCodec.DecodeRegionPayload(region);
                _snapshotMutationStore.Refresh(in _table, in _pool);
                if (!_snapshotMutationStore.TryApplySemanticSnapshot(
                        region.Coord, semanticPayload, region.SemanticHash, createIfMissing: true))
                    throw new InvalidDataException(
                        $"Storage rejected baked showcase region {region.Coord}. " +
                        "The bake may be stale or the runtime BrickPool tier may be too small.");
                _generated.Add(region.Coord);
            }

            // Recreate only cheap deterministic landmark metadata. The expensive authored voxel
            // result is already in storage; marking the plan complete prevents StepStreaming from
            // ever starting a castle build for this session.
            QueueLandmarks();
            if (!_castleTerrainQueued)
                throw new InvalidDataException("Could not reconstruct showcase castle metadata.");
            for (int i = 0; i < _castleRegions.Count; i++)
                if (!_generated.Contains(_castleRegions[i]))
                    throw new InvalidDataException(
                        $"Showcase bake is missing required castle region {_castleRegions[i]}.");

            _castlePlan = _pendingCastlePlan;
            _hasCastlePlan = true;
            _castleBuild = null;
            _castleTrapdoorOpen = false;
            _castleFrontGateOpen = false;
            _deferredFeatureRegions.Clear();
            _pendingFeatureRegions.Clear();
            _featureBuild?.Dispose();
            _featureBuild = null;

            CastleVoxels = bake.CastleVoxels;
            FeatureVoxelsBuilt = bake.FeatureVoxels;
            FeatureInstancesBuilt = bake.FeatureInstances;
            RestoreGalleryMetadata(bake);
            ReferenceArchMin = bake.ReferenceArchMin;
            ReferenceArchMax = bake.ReferenceArchMax;
            RegionsGenerated = _generated.Count;
            RegionsEvicted = 0;
            LastGenerateMs = 0;
            LastFeatureMs = 0;
            LastCastleStage = 0;
            LastCastleStageMs = 0;
            MaxCastleStage = 0;
            MaxCastleStageMs = 0;
            BuildCastlePresentationLights(in _castlePlan);

            // Rebuild presentation metadata and publish one current-state notification per region
            // after all snapshots exist. Consumers never observe a half-restored castle.
            foreach (int3 coord in _generated)
            {
                CaptureFarField(coord);
                _changes.PublishRegion(coord, VoxelChangeKind.All);
            }
        }

        private void EnsureFreshForBake()
        {
            if (_generated.Count != 0 || _gen.Active || RegionsGenerated != 0
                || _pendingLoads.Count != 0 || _pendingFeatureRegions.Count != 0
                || _featureBuild != null || _castleBuild != null || _castleTerrainQueued
                || _hasCastlePlan)
                throw new InvalidOperationException(
                    "Showcase baking/loading requires a fresh ShowcaseWorld instance.");
        }

        private static int CompareRegionCoords(int3 a, int3 b)
        {
            int y = a.y.CompareTo(b.y);
            if (y != 0) return y;
            int z = a.z.CompareTo(b.z);
            if (z != 0) return z;
            return a.x.CompareTo(b.x);
        }
    }
}
