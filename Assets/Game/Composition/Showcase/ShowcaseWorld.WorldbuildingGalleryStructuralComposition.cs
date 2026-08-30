using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Materials.Api;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Production-path proof content for typed structural socket composition. Each exhibit is a
    /// bounded explicit catalogue and is rasterised through the same FeatureGeneration path used by
    /// streamed regions. The checked-in gallery bake predates this content, so the ensure entrypoint
    /// also acts as a bounded stale-bake repair until a future bake contains these structures.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        public const int WorldbuildingGalleryStructuralProofCaseCount = 4;
        public const int WorldbuildingGalleryStructuralTraversalCount = 3;

        private const ulong BridgeTag = 1UL << 40;
        private const ulong WallTag = 1UL << 41;
        private const ulong TowerTag = 1UL << 42;
        private const ulong CliffTag = 1UL << 43;
        private const ulong FacadeTag = 1UL << 44;
        private const ulong RoofTag = 1UL << 45;
        private const ulong DetailTag = 1UL << 46;

        private static readonly string[] s_StructuralProofNames =
        {
            "Typed monumental bridge",
            "Typed castle assembly",
            "Typed cliff settlement",
            "Typed facade and roof variants",
        };

        private GalleryStructuralProofMetrics[] _structuralProofMetrics;
        private float3[] _structuralProofCentres;
        private float3[] _structuralTraversalStarts;
        private float3[] _structuralTraversalEnds;
        private StructuralAttachmentRejectReason _bridgeNegativeReject;
        private StructuralAttachmentRejectReason _cliffNegativeReject;
        private int _bridgeTerrainRelief;
        private int _archPrimitiveBaseline;
        private double _structuralAuthoringMs;

        public readonly struct GalleryStructuralProofMetrics
        {
            public readonly string Name;
            public readonly StructuralCompositionResult Result;
            public readonly int ChildCount;
            public readonly int PrimitiveCost;
            public readonly int VoxelCost;
            public readonly int RegionsVisited;
            public readonly int InstancesRasterised;
            public readonly int VoxelsWritten;
            public readonly int3 BoundsMin;
            public readonly int3 BoundsMax;
            public readonly ulong GraphHash;

            public GalleryStructuralProofMetrics(
                string name,
                in StructuralCompositionReport plan,
                in FeatureCatalogueBuildResult build)
            {
                Name = name;
                Result = plan.Result;
                ChildCount = plan.ChildCount;
                PrimitiveCost = plan.PrimitiveCost;
                VoxelCost = plan.VoxelCost;
                RegionsVisited = build.RegionsVisited;
                InstancesRasterised = build.InstancesRasterised;
                VoxelsWritten = build.VoxelsWritten;
                BoundsMin = plan.BoundsMin;
                BoundsMax = plan.BoundsMax;
                GraphHash = plan.GraphHash;
            }
        }

        public readonly struct GalleryStructuralTraversalReport
        {
            public readonly bool Reached;
            public readonly int Steps;
            public readonly float StartDistanceMetres;
            public readonly float EndDistanceMetres;
            public readonly Vector3 FinalFeetPosition;

            public GalleryStructuralTraversalReport(
                bool reached,
                int steps,
                float startDistanceMetres,
                float endDistanceMetres,
                Vector3 finalFeetPosition)
            {
                Reached = reached;
                Steps = steps;
                StartDistanceMetres = startDistanceMetres;
                EndDistanceMetres = endDistanceMetres;
                FinalFeetPosition = finalFeetPosition;
            }
        }

        private readonly struct BridgeSite
        {
            public readonly int X;
            public readonly int Z;
            public readonly int DeckY;
            public readonly int Relief;

            public BridgeSite(int x, int z, int deckY, int relief)
            {
                X = x;
                Z = z;
                DeckY = deckY;
                Relief = relief;
            }
        }

        private readonly struct CliffSite
        {
            public readonly int X;
            public readonly int Z;
            public readonly int LowY;
            public readonly int Rise;

            public CliffSite(int x, int z, int lowY, int rise)
            {
                X = x;
                Z = z;
                LowY = lowY;
                Rise = rise;
            }
        }

        private sealed class ProofDefinition
        {
            public string Name;
            public FeatureKind Kind = FeatureKind.Structure;
            public int3 Footprint;
            public StructuralPieceSpec Piece;
            public SlotSpec[] Slots = Array.Empty<SlotSpec>();
            public int[] Program = Array.Empty<int>();
            public int MaxPrimitives;
            public byte Material;
        }

        private sealed class ProgramWriter
        {
            private readonly List<int> _program = new();

            public ProgramWriter Box(int3 min, int3 size, byte material, PrimitiveMode mode = PrimitiveMode.Fill)
            {
                Op(ShapeOp.EmitBox,
                    min.x, min.y, min.z,
                    size.x, size.y, size.z,
                    material, 0, 0, (int)mode);
                return this;
            }

            public ProgramWriter Ramp(int3 min, int3 size, int axis, byte material)
            {
                Op(ShapeOp.EmitRamp,
                    min.x, min.y, min.z,
                    size.x, size.y, size.z,
                    axis, material, 0, 0, (int)PrimitiveMode.Fill);
                return this;
            }

            public ProgramWriter CallSlot(int localSlot)
            {
                Op(ShapeOp.CallSlot, localSlot);
                return this;
            }

            public int[] Finish()
            {
                Op(ShapeOp.End);
                return _program.ToArray();
            }

            private void Op(ShapeOp op, params int[] operands)
            {
                _program.Add((int)op);
                _program.Add(0);
                for (int i = 0; i < operands.Length; i++) _program.Add(operands[i]);
            }
        }

        public string WorldbuildingGalleryStructuralProofName(int index) =>
            s_StructuralProofNames[NormalizeStructuralProofIndex(index)];

        public GalleryStructuralProofMetrics WorldbuildingGalleryStructuralProofMetrics(int index)
        {
            EnsureStructuralMetrics();
            return _structuralProofMetrics[NormalizeStructuralProofIndex(index)];
        }

        public float3 WorldbuildingGalleryStructuralProofCentre(int index)
        {
            EnsureStructuralMetrics();
            return _structuralProofCentres[NormalizeStructuralProofIndex(index)];
        }

        public int WorldbuildingGalleryStructuralBridgeTerrainRelief
        {
            get { EnsureStructuralMetrics(); return _bridgeTerrainRelief; }
        }

        public int WorldbuildingGalleryStructuralArchPrimitiveBaseline
        {
            get { EnsureStructuralMetrics(); return _archPrimitiveBaseline; }
        }

        public double WorldbuildingGalleryStructuralAuthoringMilliseconds => _structuralAuthoringMs;

        public StructuralAttachmentRejectReason WorldbuildingGalleryStructuralBridgeNegativeReject
        {
            get { EnsureStructuralMetrics(); return _bridgeNegativeReject; }
        }

        public StructuralAttachmentRejectReason WorldbuildingGalleryStructuralCliffNegativeReject
        {
            get { EnsureStructuralMetrics(); return _cliffNegativeReject; }
        }

        public bool HasWorldbuildingGalleryStructuralCompositionContent()
        {
            EnsureStructuralMetrics(author: false);
            for (int i = 0; i < _structuralProofCentres.Length; i++)
            {
                int x = (int)math.round(_structuralProofCentres[i].x / VoxelSize);
                int z = (int)math.round(_structuralProofCentres[i].z / VoxelSize);
                if (!HasBuiltContentAbove(x, z)) return false;
            }
            return true;
        }

        public void EnsureWorldbuildingGalleryStructuralCompositionBlocking()
        {
            EnsureStructuralMetrics(author: true);
        }

        public void PrepareWorldbuildingGalleryStructuralTraversal(int index, out Vector3 start, out Vector3 end)
        {
            EnsureStructuralMetrics(author: true);
            int route = index % WorldbuildingGalleryStructuralTraversalCount;
            if (route < 0) route += WorldbuildingGalleryStructuralTraversalCount;

            start = ToVector3(_structuralTraversalStarts[route]);
            end = ToVector3(_structuralTraversalEnds[route]);
            PreloadTraversal(start, end);
        }

        private void EnsureStructuralMetrics(bool author = false)
        {
            if (_structuralProofMetrics != null && (!author || HasStructuralProbeVoxels())) return;

            _structuralProofMetrics = new GalleryStructuralProofMetrics[WorldbuildingGalleryStructuralProofCaseCount];
            _structuralProofCentres = new float3[WorldbuildingGalleryStructuralProofCaseCount];
            _structuralTraversalStarts = new float3[WorldbuildingGalleryStructuralTraversalCount];
            _structuralTraversalEnds = new float3[WorldbuildingGalleryStructuralTraversalCount];

            var timer = Stopwatch.StartNew();
            BridgeSite bridge = FindBridgeSite();
            _bridgeTerrainRelief = bridge.Relief;
            BuildBridgeProof(bridge, author);

            BuildCastleProof(new int3(-2900,
                TerrainQuery.HeightAt(-2900, 120, Seed) + 2, 120), author);

            CliffSite cliff = FindCliffSite();
            BuildCliffSettlementProof(cliff, author);

            int facadeY = TerrainQuery.HeightAt(-2500, 1180, Seed) + 2;
            BuildFacadeProofs(new int3(-2500, facadeY, 1180), author);
            timer.Stop();
            _structuralAuthoringMs = timer.Elapsed.TotalMilliseconds;

            _archPrimitiveBaseline = 15;

            UnityEngine.Debug.Log(
                $"STRUCTURAL_GALLERY authored={author} elapsedMs={_structuralAuthoringMs:0.###} " +
                $"bridgeRelief={_bridgeTerrainRelief}v bridgePrimitiveCost={_structuralProofMetrics[0].PrimitiveCost} " +
                $"archBaselinePrimitives={_archPrimitiveBaseline} " +
                $"bridgeReject={_bridgeNegativeReject} cliffReject={_cliffNegativeReject}");
        }

        private bool HasStructuralProbeVoxels()
        {
            if (_structuralProofCentres == null) return false;
            for (int i = 0; i < _structuralProofCentres.Length; i++)
            {
                int x = (int)math.round(_structuralProofCentres[i].x / VoxelSize);
                int z = (int)math.round(_structuralProofCentres[i].z / VoxelSize);
                if (!HasBuiltContentAbove(x, z)) return false;
            }
            return true;
        }

        private void BuildBridgeProof(BridgeSite site, bool author)
        {
            int3 rootPosition = new(site.X + 450, site.DeckY, site.Z);
            using FeatureCatalogue catalogue = CreateBridgeCatalogue(rootPosition);
            StructuralCompositionReport plan = Plan(in catalogue, 0);
            RequireOk("bridge", in plan);
            FeatureCatalogueBuildResult build = BuildIfNeeded(in catalogue, in plan, author);
            _structuralProofMetrics[0] = new GalleryStructuralProofMetrics(s_StructuralProofNames[0], in plan, in build);
            _structuralProofCentres[0] = new float3(site.X + 610, site.DeckY + 35, site.Z + 40) * VoxelSize;
            _structuralTraversalStarts[0] = new float3(site.X + 35, site.DeckY + 30, site.Z + 40) * VoxelSize;
            _structuralTraversalEnds[0] = new float3(site.X + 1185, site.DeckY + 30, site.Z + 40) * VoxelSize;

            FeatureCatalogue mutableCatalogue = catalogue;
            FeatureDefinition original = mutableCatalogue.Definitions[1];
            FeatureDefinition incompatible = original;
            StructuralPieceSpec piece = incompatible.StructuralPiece;
            piece.Offers = 1UL << 55;
            piece.Accepts = 1UL << 55;
            incompatible.StructuralPiece = piece;
            mutableCatalogue.Definitions[1] = incompatible;
            using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
            using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
            StructuralCompositionPlanner.ExpandRoot(in mutableCatalogue, Seed, 0,
                mutableCatalogue.ExplicitPlacements[0], instances, decisions);
            _bridgeNegativeReject = FirstRejected(decisions);
            mutableCatalogue.Definitions[1] = original;
        }

        private FeatureCatalogue CreateBridgeCatalogue(int3 rootPosition)
        {
            byte stone = GameMaterialIds.MasonryLarge;
            byte rail = GameMaterialIds.DarkStone;
            var defs = new[]
            {
                Def("bridge-core", new int3(320, 60, 80),
                    Piece(0x53544201u, StructuralSocketRole.BridgeSpan | StructuralSocketRole.Traversal,
                        BridgeTag, int3.zero, Facing.West),
                    new ProgramWriter()
                        .Box(new int3(0, 20, 0), new int3(320, 8, 80), stone)
                        .Box(new int3(0, 28, 0), new int3(320, 10, 4), rail)
                        .Box(new int3(0, 28, 76), new int3(320, 10, 4), rail)
                        .CallSlot(0).CallSlot(1).CallSlot(2).Finish(), 3, stone,
                    Slot("east-span", 0x53544211u, StructuralSocketRole.BridgeSpan, BridgeTag,
                        new int3(320, 0, 0), Facing.East, 1, required: true),
                    Slot("west-span", 0x53544212u, StructuralSocketRole.BridgeSpan, BridgeTag,
                        int3.zero, Facing.West, 2, required: true),
                    new SlotSpec
                    {
                        Name = "repeating-supports", SocketId = 0x53544213u,
                        Role = StructuralSocketRole.Support | StructuralSocketRole.TerrainAnchor,
                        Offers = BridgeTag, Accepts = BridgeTag,
                        LocalPosition = new int3(80, 20, 40), Facing = Facing.Down,
                        DefinitionId = 3,
                        LocalMin = new int3(80, 20, 40), LocalMax = new int3(319, 20, 40),
                        CountMin = 3, CountMax = 3, Capacity = 3, Spacing = 80,
                        Flags = StructuralSocketFlags.Required | StructuralSocketFlags.RequireTerrainSupport,
                        SupportProbeMin = new int3(-8, -240, -8),
                        SupportProbeMax = new int3(8, 0, 8), MinimumSupportContacts = 1,
                    }),
                Def("bridge-east-span", new int3(450, 60, 80),
                    Piece(0x53544202u, StructuralSocketRole.BridgeSpan | StructuralSocketRole.Traversal,
                        BridgeTag, int3.zero, Facing.West),
                    new ProgramWriter()
                        .Box(new int3(0, 20, 0), new int3(450, 8, 80), stone)
                        .Box(new int3(0, 28, 0), new int3(450, 10, 4), rail)
                        .Box(new int3(0, 28, 76), new int3(450, 10, 4), rail)
                        .CallSlot(0).Finish(), 3, stone,
                    Slot("east-road-continuation", 0x53544221u, StructuralSocketRole.Traversal,
                        BridgeTag, new int3(450, 20, 0), Facing.East, 4, required: false, count: 0)),
                Def("bridge-west-span", new int3(450, 60, 80),
                    Piece(0x53544203u, StructuralSocketRole.BridgeSpan | StructuralSocketRole.Traversal,
                        BridgeTag, new int3(450, 0, 0), Facing.East),
                    new ProgramWriter()
                        .Box(new int3(0, 20, 0), new int3(450, 8, 80), stone)
                        .Box(new int3(0, 28, 0), new int3(450, 10, 4), rail)
                        .Box(new int3(0, 28, 76), new int3(450, 10, 4), rail)
                        .CallSlot(0).Finish(), 3, stone,
                    Slot("west-road-continuation", 0x53544222u, StructuralSocketRole.Traversal,
                        BridgeTag, int3.zero, Facing.West, 4, required: false, count: 0)),
                Def("bridge-pier", new int3(20, 181, 20),
                    Piece(0x53544204u, StructuralSocketRole.Support | StructuralSocketRole.TerrainAnchor,
                        BridgeTag, new int3(10, 180, 10), Facing.Up),
                    new ProgramWriter().Box(int3.zero, new int3(20, 181, 20), stone).Finish(), 1, stone),
                Def("road-continuation-contract", new int3(40, 8, 80),
                    Piece(0x53544205u, StructuralSocketRole.Traversal, BridgeTag, int3.zero, Facing.West),
                    new ProgramWriter().Finish(), 0, stone),
            };
            return BuildCatalogue(defs, rootPosition);
        }

        private void BuildCastleProof(int3 origin, bool author)
        {
            using FeatureCatalogue catalogue = CreateCastleCatalogue(origin);
            StructuralCompositionReport plan = Plan(in catalogue, 0);
            RequireOk("castle", in plan);
            FeatureCatalogueBuildResult build = BuildIfNeeded(in catalogue, in plan, author);
            _structuralProofMetrics[1] = new GalleryStructuralProofMetrics(s_StructuralProofNames[1], in plan, in build);
            _structuralProofCentres[1] = new float3(origin.x, origin.y + 70, origin.z + 60) * VoxelSize;
            _structuralTraversalStarts[1] = new float3(origin.x + 80, origin.y + 8, origin.z - 30) * VoxelSize;
            _structuralTraversalEnds[1] = new float3(origin.x + 80, origin.y + 8, origin.z + 150) * VoxelSize;
        }

        private FeatureCatalogue CreateCastleCatalogue(int3 origin)
        {
            byte stone = GameMaterialIds.MasonryMedium;
            byte cap = GameMaterialIds.DarkStone;
            var defs = new[]
            {
                Def("castle-gatehouse", new int3(160, 100, 120),
                    Piece(0x53544301u, StructuralSocketRole.Gate | StructuralSocketRole.Building,
                        WallTag, int3.zero, Facing.South),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(160, 78, 120), stone)
                        .Box(new int3(64, 0, 0), new int3(32, 50, 120), 0, PrimitiveMode.Carve)
                        .Box(new int3(0, 78, 0), new int3(160, 12, 120), cap)
                        .CallSlot(0).CallSlot(1).Finish(), 3, stone,
                    Slot("east-wall", 0x53544311u, StructuralSocketRole.Wall, WallTag,
                        new int3(160, 0, 40), Facing.East, 1, required: true),
                    Slot("west-wall", 0x53544312u, StructuralSocketRole.Wall, WallTag,
                        new int3(0, 0, 40), Facing.West, 2, required: true)),
                Def("castle-east-wall", new int3(220, 70, 40),
                    Piece(0x53544302u, StructuralSocketRole.Wall, WallTag, int3.zero, Facing.West),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(220, 55, 40), stone)
                        .Box(new int3(0, 55, 0), new int3(220, 10, 40), cap)
                        .CallSlot(0).Finish(), 2, stone,
                    Slot("east-tower", 0x53544321u, StructuralSocketRole.Tower, TowerTag,
                        new int3(220, 0, 20), Facing.East, 3, required: true)),
                Def("castle-west-wall", new int3(220, 70, 40),
                    Piece(0x53544303u, StructuralSocketRole.Wall, WallTag,
                        new int3(220, 0, 0), Facing.East),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(220, 55, 40), stone)
                        .Box(new int3(0, 55, 0), new int3(220, 10, 40), cap)
                        .CallSlot(0).Finish(), 2, stone,
                    Slot("west-tower", 0x53544322u, StructuralSocketRole.Tower, TowerTag,
                        new int3(0, 0, 20), Facing.West, 4, required: true)),
                TowerDef("castle-east-tower", 0x53544304u, Facing.West, stone, cap),
                TowerDef("castle-west-tower", 0x53544305u, Facing.East, stone, cap,
                    ingress: new int3(80, 0, 40)),
            };
            return BuildCatalogue(defs, origin);
        }

        private static ProofDefinition TowerDef(string name, uint pieceId, Facing facing,
            byte stone, byte cap, int3 ingress = default)
        {
            return Def(name, new int3(80, 150, 80),
                Piece(pieceId, StructuralSocketRole.Tower, TowerTag, ingress, facing),
                new ProgramWriter()
                    .Box(int3.zero, new int3(80, 130, 80), stone)
                    .Box(new int3(0, 130, 0), new int3(80, 12, 80), cap)
                    .Finish(), 2, stone);
        }

        private void BuildCliffSettlementProof(CliffSite site, bool author)
        {
            int3 origin = new(site.X, site.LowY + 4, site.Z);
            using FeatureCatalogue catalogue = CreateCliffCatalogue(origin, site.Rise);
            StructuralCompositionReport plan = Plan(in catalogue, 0);
            RequireOk("cliff settlement", in plan);
            FeatureCatalogueBuildResult build = BuildIfNeeded(in catalogue, in plan, author);
            _structuralProofMetrics[2] = new GalleryStructuralProofMetrics(s_StructuralProofNames[2], in plan, in build);
            _structuralProofCentres[2] = new float3(site.X + 300, site.LowY + site.Rise + 60, site.Z + 40) * VoxelSize;
            _structuralTraversalStarts[2] = new float3(site.X + 40, site.LowY + 18, site.Z + 40) * VoxelSize;
            _structuralTraversalEnds[2] = new float3(site.X + 500, site.LowY + site.Rise + 28, site.Z + 40) * VoxelSize;

            FeatureCatalogue mutableCatalogue = catalogue;
            SlotSpec original = mutableCatalogue.Slots[1];
            SlotSpec unsupported = original;
            unsupported.SupportProbeMin += new int3(0, 100000, 0);
            unsupported.SupportProbeMax += new int3(0, 100000, 0);
            mutableCatalogue.Slots[1] = unsupported;
            using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
            using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
            StructuralCompositionPlanner.ExpandRoot(in mutableCatalogue, Seed, 0,
                mutableCatalogue.ExplicitPlacements[0], instances, decisions);
            _cliffNegativeReject = FirstRejected(decisions);
            mutableCatalogue.Slots[1] = original;
        }

        private FeatureCatalogue CreateCliffCatalogue(int3 origin, int rise)
        {
            int rampHeight = math.max(24, rise + 12);
            byte stone = GameMaterialIds.MasonrySmall;
            byte wood = GameMaterialIds.Wood;
            var defs = new[]
            {
                Def("cliff-lower-platform", new int3(180, 24, 120),
                    Piece(0x53544401u, StructuralSocketRole.Platform | StructuralSocketRole.TerrainAnchor,
                        CliffTag, int3.zero, Facing.West),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(180, 12, 120), stone)
                        .CallSlot(0).Finish(), 1, stone,
                    Slot("cliff-ramp", 0x53544411u,
                        StructuralSocketRole.Traversal | StructuralSocketRole.VerticalConnection,
                        CliffTag, new int3(180, 12, 20), Facing.East, 1, required: true)),
                Def("cliff-ramp", new int3(260, rampHeight, 80),
                    Piece(0x53544402u,
                        StructuralSocketRole.Traversal | StructuralSocketRole.VerticalConnection,
                        CliffTag, int3.zero, Facing.West),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(260, 1, 80), stone)
                        .Ramp(int3.zero, new int3(260, math.max(8, rise + 4), 80), 0, stone)
                        .CallSlot(0).Finish(), 2, stone,
                    new SlotSpec
                    {
                        Name = "upper-terrain-platform", SocketId = 0x53544421u,
                        Role = StructuralSocketRole.Platform | StructuralSocketRole.TerrainAnchor,
                        Offers = CliffTag, Accepts = CliffTag,
                        LocalPosition = new int3(260, rise - 8, 0), Facing = Facing.East,
                        DefinitionId = 2,
                        LocalMin = new int3(260, rise - 8, 0), LocalMax = new int3(260, rise - 8, 0),
                        CountMin = 1, CountMax = 1, Capacity = 1,
                        Flags = StructuralSocketFlags.Required | StructuralSocketFlags.RequireTerrainSupport,
                        SupportProbeMin = new int3(-10, -22, -10),
                        SupportProbeMax = new int3(10, 22, 10), MinimumSupportContacts = 1,
                    }),
                Def("cliff-upper-platform", new int3(180, 24, 120),
                    Piece(0x53544403u, StructuralSocketRole.Platform | StructuralSocketRole.TerrainAnchor,
                        CliffTag, int3.zero, Facing.West),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(180, 12, 120), stone)
                        .CallSlot(0).Finish(), 1, stone,
                    Slot("upper-building", 0x53544431u, StructuralSocketRole.Building, CliffTag,
                        new int3(40, 12, 20), Facing.Up, 3, required: true)),
                Def("cliff-house", new int3(100, 120, 80),
                    Piece(0x53544404u, StructuralSocketRole.Building, CliffTag,
                        int3.zero, Facing.Down),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(100, 80, 80), wood)
                        .Box(new int3(8, 80, 8), new int3(84, 24, 64), GameMaterialIds.Slate)
                        .Finish(), 2, wood),
            };
            return BuildCatalogue(defs, origin);
        }

        private void BuildFacadeProofs(int3 firstOrigin, bool author)
        {
            GalleryStructuralProofMetrics a = BuildFacadeVariant(firstOrigin, false, author);
            int3 secondOrigin = firstOrigin + new int3(300, 0, 0);
            GalleryStructuralProofMetrics b = BuildFacadeVariant(secondOrigin, true, author);

            var aggregatePlan = new StructuralCompositionReport
            {
                Result = a.Result == StructuralCompositionResult.Ok && b.Result == StructuralCompositionResult.Ok
                    ? StructuralCompositionResult.Ok : StructuralCompositionResult.MalformedProgram,
                ChildCount = a.ChildCount + b.ChildCount,
                PrimitiveCost = a.PrimitiveCost + b.PrimitiveCost,
                VoxelCost = a.VoxelCost + b.VoxelCost,
                BoundsMin = math.min(a.BoundsMin, b.BoundsMin),
                BoundsMax = math.max(a.BoundsMax, b.BoundsMax),
                GraphHash = a.GraphHash,
            };
            var aggregateBuild = new FeatureCatalogueBuildResult(
                a.RegionsVisited + b.RegionsVisited,
                a.InstancesRasterised + b.InstancesRasterised,
                a.VoxelsWritten + b.VoxelsWritten);
            _structuralProofMetrics[3] = new GalleryStructuralProofMetrics(
                s_StructuralProofNames[3], in aggregatePlan, in aggregateBuild);
            _structuralProofCentres[3] = new float3(firstOrigin.x + 90, firstOrigin.y + 95,
                firstOrigin.z + 60) * VoxelSize;
        }

        private GalleryStructuralProofMetrics BuildFacadeVariant(int3 origin, bool ornate, bool author)
        {
            using FeatureCatalogue catalogue = CreateFacadeCatalogue(origin, ornate);
            StructuralCompositionReport plan = Plan(in catalogue, 0);
            RequireOk(ornate ? "facade ornate" : "facade civic", in plan);
            FeatureCatalogueBuildResult build = BuildIfNeeded(in catalogue, in plan, author);
            return new GalleryStructuralProofMetrics(ornate ? "ornate" : "civic", in plan, in build);
        }

        private FeatureCatalogue CreateFacadeCatalogue(int3 origin, bool ornate)
        {
            byte body = ornate ? GameMaterialIds.DarkStone : GameMaterialIds.MasonryMedium;
            byte roof = ornate ? GameMaterialIds.Slate : GameMaterialIds.Tile;
            byte trim = ornate ? GameMaterialIds.Gold : GameMaterialIds.Wood;
            int roofLift = ornate ? 48 : 32;
            var defs = new[]
            {
                Def(ornate ? "ornate-building" : "civic-building", new int3(180, 160, 120),
                    Piece(ornate ? 0x53544511u : 0x53544501u,
                        StructuralSocketRole.Building, FacadeTag | RoofTag, int3.zero, Facing.South),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(180, 150, 120), body)
                        .Box(new int3(12, 20, 116), new int3(156, 90, 4), trim)
                        .CallSlot(0).CallSlot(1).Finish(), 2, body,
                    Slot("semantic-facade", 0x53544521u, StructuralSocketRole.Facade, FacadeTag,
                        new int3(0, 0, 120), Facing.North, 1, required: true),
                    Slot("semantic-roof", 0x53544522u, StructuralSocketRole.Roof, RoofTag,
                        new int3(0, 160, 0), Facing.Up, 2, required: true)),
                Def("bounded-facade", new int3(180, 130, 20),
                    Piece(ornate ? 0x53544512u : 0x53544502u,
                        StructuralSocketRole.Facade, FacadeTag, int3.zero, Facing.South),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(180, 120, 12), trim)
                        .Box(new int3(24, 24, 0), new int3(24, 78, 18), body)
                        .Box(new int3(132, 24, 0), new int3(24, 78, 18), body)
                        .CallSlot(0).Finish(), 3, trim,
                    Slot("balcony", 0x53544531u, StructuralSocketRole.Facade, DetailTag,
                        new int3(50, 56, 20), Facing.North, 3, required: true)),
                Def("bounded-roof", new int3(180, 64, 120),
                    Piece(ornate ? 0x53544513u : 0x53544503u,
                        StructuralSocketRole.Roof, RoofTag, int3.zero, Facing.Down),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(180, 12, 120), roof)
                        .Box(new int3(16, 12, 12), new int3(148, roofLift, 96), roof)
                        .CallSlot(0).Finish(), 2, roof,
                    Slot("dormer", 0x53544532u, StructuralSocketRole.Roof | StructuralSocketRole.Facade,
                        DetailTag, new int3(62, 12, 44), Facing.Up, 4, required: true)),
                Def("bounded-balcony", new int3(80, 20, 30),
                    Piece(ornate ? 0x53544514u : 0x53544504u,
                        StructuralSocketRole.Facade, DetailTag, int3.zero, Facing.South),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(80, 8, 30), trim)
                        .Box(new int3(0, 8, 26), new int3(80, 10, 4), trim)
                        .Finish(), 2, trim),
                Def("bounded-dormer", new int3(56, 48, 40),
                    Piece(ornate ? 0x53544515u : 0x53544505u,
                        StructuralSocketRole.Roof | StructuralSocketRole.Facade,
                        DetailTag, int3.zero, Facing.Down),
                    new ProgramWriter()
                        .Box(int3.zero, new int3(56, 38, 40), body)
                        .Box(new int3(8, 10, 36), new int3(40, 20, 4), GameMaterialIds.LitWindow)
                        .Finish(), 2, body),
            };
            return BuildCatalogue(defs, origin);
        }

        private FeatureCatalogueBuildResult BuildIfNeeded(
            in FeatureCatalogue catalogue,
            in StructuralCompositionReport plan,
            bool author)
        {
            if (!author) return default;
            PreloadBounds(plan.BoundsMin, plan.BoundsMax);
            return StructuresComposition.BuildExplicitFeatureCatalogue(_storage, in catalogue, Seed);
        }

        private void PreloadBounds(int3 min, int3 max)
        {
            int edge = VoxelGrid.RegionVoxelEdge;
            int3 first = (int3)math.floor((float3)min / edge);
            int3 last = (int3)math.floor((float3)(max - 1) / edge);
            for (int y = first.y; y <= last.y; y++)
            for (int z = first.z; z <= last.z; z++)
            for (int x = first.x; x <= last.x; x++)
                GenerateRegionBlocking(new int3(x, y, z));
        }

        private void PreloadTraversal(Vector3 start, Vector3 end)
        {
            const int samples = 12;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, i / (float)samples);
                int3 region = RegionAt(p);
                for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                    GenerateRegionBlocking(region + new int3(x, 0, z));
            }
        }

        private StructuralCompositionReport Plan(in FeatureCatalogue catalogue, int rootDefinitionId)
        {
            using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
            return StructuralCompositionPlanner.ExpandRoot(
                in catalogue, Seed, rootDefinitionId, catalogue.ExplicitPlacements[0], instances);
        }

        private static StructuralAttachmentRejectReason FirstRejected(
            NativeList<StructuralAttachmentDecision> decisions)
        {
            for (int i = 0; i < decisions.Length; i++)
                if (!decisions[i].Accepted) return decisions[i].Rejection;
            return StructuralAttachmentRejectReason.None;
        }

        private static void RequireOk(string proof, in StructuralCompositionReport report)
        {
            if (report.Result != StructuralCompositionResult.Ok)
                throw new InvalidOperationException(
                    $"Typed structural gallery {proof} failed composition: {report.Result}.");
        }

        private FeatureCatalogue BuildCatalogue(ProofDefinition[] definitions, int3 rootPosition)
        {
            int slots = 0;
            int programLength = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                slots += definitions[i].Slots.Length;
                programLength += definitions[i].Program.Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions.Length, 1, 0, 0, slots, programLength,
                definitions.Length, 1, 0, Allocator.Temp);

            int slotOffset = 0;
            int pc = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                ProofDefinition spec = definitions[i];
                catalogue.Materials[i] = spec.Material;
                for (int p = 0; p < spec.Program.Length; p++) catalogue.Program[pc + p] = spec.Program[p];
                for (int s = 0; s < spec.Slots.Length; s++) catalogue.Slots[slotOffset + s] = spec.Slots[s];
                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = spec.Name,
                    Kind = spec.Kind,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = rootPosition.y,
                    Footprint = spec.Footprint,
                    MaxSlope = 0,
                    Precedence = 200,
                    StructuralPiece = spec.Piece,
                    SlotOffset = slotOffset,
                    SlotCount = spec.Slots.Length,
                    ProgramOffset = pc,
                    ProgramLength = spec.Program.Length,
                    MaterialOffset = i,
                    MaterialCount = 1,
                    MaxPrimitives = spec.MaxPrimitives,
                };
                slotOffset += spec.Slots.Length;
                pc += spec.Program.Length;
            }

            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = rootPosition,
                Orientation = 0,
            };
            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = 0,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 1,
                AcceptProbability = 65536,
                MinAltitude = -FeatureBudget.MaxFootprintVoxels,
                MaxAltitude = FeatureBudget.MaxFootprintVoxels,
                MaxSlope = 0,
                MinSpacing = 0,
                ClusterMin = 1,
                ClusterMax = 1,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException($"Typed structural gallery catalogue rejected: {result}.");
            }
            return catalogue;
        }

        private static ProofDefinition Def(string name, int3 footprint, StructuralPieceSpec piece,
            int[] program, int maxPrimitives, byte material, params SlotSpec[] slots) => new()
        {
            Name = name,
            Footprint = footprint,
            Piece = piece,
            Program = program,
            MaxPrimitives = maxPrimitives,
            Material = material,
            Slots = slots ?? Array.Empty<SlotSpec>(),
        };

        private static StructuralPieceSpec Piece(uint id, StructuralSocketRole role, ulong tag,
            int3 ingress, Facing facing) => new()
        {
            PieceId = id,
            Role = role,
            Offers = tag,
            Accepts = tag,
            LocalPosition = ingress,
            Facing = facing,
            ClearanceMin = int3.zero,
            ClearanceMax = int3.zero,
        };

        private static SlotSpec Slot(string name, uint socketId, StructuralSocketRole role,
            ulong tag, int3 position, Facing facing, int definitionId, bool required, int count = 1) => new()
        {
            Name = name,
            SocketId = socketId,
            Role = role,
            Offers = tag,
            Accepts = tag,
            LocalPosition = position,
            Facing = facing,
            DefinitionId = definitionId,
            LocalMin = position,
            LocalMax = position,
            ClearanceMin = int3.zero,
            ClearanceMax = int3.zero,
            CountMin = count,
            CountMax = count,
            Capacity = 1,
            Spacing = 0,
            Flags = required ? StructuralSocketFlags.Required : StructuralSocketFlags.None,
        };

        private BridgeSite FindBridgeSite()
        {
            const int totalSpan = 1220;
            int bestX = -3600, bestZ = -700, bestRelief = int.MinValue, bestDeck = BaseHeight + 80;
            for (int z = -900; z <= 900; z += 120)
            for (int x = -3900; x <= -1900; x += 120)
            {
                int left = TerrainQuery.HeightAt(x, z, Seed);
                int right = TerrainQuery.HeightAt(x + totalSpan, z, Seed);
                int minimum = int.MaxValue;
                for (int i = 1; i < 8; i++)
                    minimum = math.min(minimum, TerrainQuery.HeightAt(x + totalSpan * i / 8, z, Seed));
                int relief = math.min(left, right) - minimum;
                if (relief <= bestRelief) continue;
                bestRelief = relief;
                bestX = x;
                bestZ = z;
                bestDeck = math.max(left, right) + 36;
            }
            return new BridgeSite(bestX, bestZ, bestDeck, bestRelief);
        }

        private CliffSite FindCliffSite()
        {
            const int run = 440;
            int bestX = -3400, bestZ = 760, bestLow = BaseHeight, bestRise = 24;
            int bestScore = int.MinValue;
            for (int z = 520; z <= 1500; z += 80)
            for (int x = -3600; x <= -1900; x += 80)
            {
                int low = TerrainQuery.HeightAt(x, z, Seed);
                int high = TerrainQuery.HeightAt(x + run, z, Seed);
                int rise = high - low;
                if (rise < 12 || rise > 42) continue;
                if (rise <= bestScore) continue;
                bestScore = rise;
                bestX = x;
                bestZ = z;
                bestLow = low;
                bestRise = rise;
            }
            return new CliffSite(bestX, bestZ, bestLow, bestRise);
        }

        private static int NormalizeStructuralProofIndex(int index)
        {
            int normalized = index % WorldbuildingGalleryStructuralProofCaseCount;
            return normalized < 0 ? normalized + WorldbuildingGalleryStructuralProofCaseCount : normalized;
        }

        private static Vector3 ToVector3(float3 value) => new(value.x, value.y, value.z);

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
