using NUnit.Framework;
using System.Reflection;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Runtime.Emitters;
using VoxelEngine.Storage.Runtime;
using CoreSurfaceCompatibility = VoxelEngine.Storage.Runtime.SurfaceCompatibility;
using CoreSurfaceReconstruction = VoxelEngine.Storage.Runtime.SurfaceReconstruction;
using CoreSurfaceDecorationShape = VoxelEngine.Storage.Runtime.SurfaceDecorationShape;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VoxelSurfaceArchitectureTests
    {
        [Test]
        public void SurfaceJoinLookupIsSymmetricAndMissingPairsAreSharpSeams()
        {
            SurfaceCatalogue catalogue = SurfaceCatalogue.CreateBuiltIns();
            Assert.AreEqual(SurfaceCatalogue.BuiltInVersion, catalogue.Version);
            Assert.AreNotEqual(0ul, catalogue.CatalogueHash);
            Assert.AreEqual(catalogue.ComputeHash(), catalogue.CatalogueHash);
            SurfaceJoinRule ab = catalogue.GetJoin(1, 2);
            SurfaceJoinRule ba = catalogue.GetJoin(2, 1);
            Assert.True(ab.Equals(ba));
            Assert.AreEqual(CoreSurfaceCompatibility.Seam, ab.Compatibility);
            Assert.True(ab.PreserveSharpFeature);
            SurfaceStyleDefinition missing = catalogue.Get(31);
            Assert.AreEqual(CoreSurfaceReconstruction.Sharp, missing.Reconstruction);
            Assert.True(missing.PreserveSharpFeatures);
        }

        [Test]
        public void MasonryReconstructionPreservesCutStoneInsteadOfBlurringOccupancy()
        {
            SurfaceCatalogue catalogue = SurfaceCatalogue.CreateBuiltIns();
            SurfaceStyleDefinition masonry = catalogue.Get(SurfaceStyles.MasonryJoint);

            Assert.AreEqual(CoreSurfaceReconstruction.Planar, masonry.Reconstruction);
            Assert.AreEqual(0, masonry.Curvature);
            Assert.True(masonry.PreserveSharpFeatures);
        }

        [Test]
        public void SurfaceCatalogueIdentityDependsOnStableIdsNotRegistrationOrder()
        {
            var smooth = new SurfaceStyleDefinition
            {
                StableId = 1, Reconstruction = CoreSurfaceReconstruction.Smooth,
                Curvature = 255, JoinGroup = 1
            };
            var planar = new SurfaceStyleDefinition
            {
                StableId = 2, Reconstruction = CoreSurfaceReconstruction.Planar,
                JoinGroup = 2, PreserveSharpFeatures = true
            };
            SurfaceCatalogue a = default;
            a.Register(in smooth);
            a.Register(in planar);
            SurfaceCatalogue b = default;
            b.Register(in planar);
            b.Register(in smooth);
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
        }

        [Test]
        public void MutatingSealedSurfaceRulesInvalidatesTheirCatalogueHash()
        {
            SurfaceCatalogue catalogue = SurfaceCatalogue.CreateBuiltIns();
            Assert.AreNotEqual(0ul, catalogue.CatalogueHash);
            SurfaceJoinRule rule = catalogue.GetJoin(1, 1);
            rule.BlendWidth++;
            catalogue.SetJoin(1, 1, in rule);
            Assert.AreEqual(0ul, catalogue.CatalogueHash,
                "mutated rules must be rehashed before they become extractor inputs");
            Assert.AreNotEqual(0ul, catalogue.ComputeHash());
        }

        [Test]
        public void SimulationMaterialAdjacencyIsSymmetricAndSeparateFromSurfaceJoins()
        {
            MaterialAdjacencyCatalogue materials = default;
            materials.Set(6, 2, MaterialAdjacencyEffect.Supported);
            Assert.AreEqual(materials.Get(6, 2), materials.Get(2, 6));
            Assert.AreEqual(MaterialAdjacencyEffect.RejectPlacement, materials.Get(40, 1));
        }

        [Test]
        public void CoatingDoesNotReplaceBaseMaterialOrDestructionBehavior()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(8, Allocator.Temp);
            try
            {
                var stone = new VoxelCell
                {
                    BaseMaterialId = 6,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = SurfaceStyles.Rounded,
                        CoatingId = Coatings.Moss
                    }
                };
                Assert.True(VoxelAccess.SetCell(ref table, ref pool, new int3(1, 2, 3), in stone));
                VoxelCell stored = VoxelAccess.GetCell(ref table, in pool, new int3(1, 2, 3));
                Assert.AreEqual(6, stored.BaseMaterialId);
                Assert.AreEqual(Coatings.Moss, stored.Surface.CoatingId);

                MaterialPalette palette = default;
                palette.Register(6, 210, DestructionClass.Crumble,
                                 SurfaceStyles.Rounded, 1u << Coatings.Moss);
                Assert.AreEqual(DestructionClass.Crumble,
                                palette.GetDestructionClass(stored.BaseMaterialId));
                Assert.True(palette.AllowsCoating(stored.BaseMaterialId,
                                                  stored.Surface.CoatingId));
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void CompactSurfaceStorageRoundTripsEveryAuthoredChannel()
        {
            var authored = new VoxelSurfaceSemantics
            {
                StyleId = 31,
                CoatingId = 15,
                Flags = VoxelSurfaceFlags.PreserveFeature | VoxelSurfaceFlags.IntentionalSeam,
                Detail = 31,
            };

            VoxelSurfaceSemantics restored =
                VoxelSurfaceSemantics.FromStorage(authored.PackedStorage);

            Assert.AreEqual(authored, restored);
        }

        [Test]
        public void CoatingCatalogueIdentityIncludesGeometryDisplacement()
        {
            CoatingCatalogue original = CoatingCatalogue.CreateBuiltIns();
            CoatingCatalogue changed = original;
            CoatingDefinition moss = changed.Get(Coatings.Moss);
            moss.Displacement++;
            changed.Register(in moss);

            Assert.AreNotEqual(original.CatalogueHash, changed.ComputeHash());
            Assert.AreEqual(0, original.Get(Coatings.Moss).Displacement,
                "raised moss mats provide relief without changing the base solid topology");
            CoatingDefinition builtInMoss = original.Get(Coatings.Moss);
            Assert.AreEqual(CoreSurfaceDecorationShape.Clump, builtInMoss.DecorationShape);
            Assert.Greater(builtInMoss.DecorationDensity, 0);
            Assert.Greater(builtInMoss.DecorationRadiusQ4, 0);
            Assert.Greater(builtInMoss.DecorationHeightQ4, 0);
            Assert.Greater(builtInMoss.DecorationDropQ4, 0);
            Assert.GreaterOrEqual(builtInMoss.DecorationSeparation, 0,
                "zero is the intentional dense-mat spacing; negative spacing is invalid");
            Assert.AreNotEqual(0, builtInMoss.DecorationFaceMask & (1 << 3));
        }

        [Test]
        public void SurfaceDetailOperationPreservesStructureAndCoating()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(4, Allocator.Temp);
            var primitives = new NativeArray<Primitive>(1, Allocator.Temp);
            try
            {
                var original = new VoxelCell
                {
                    BaseMaterialId = 6,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = SurfaceStyles.Rounded,
                        CoatingId = Coatings.Moss,
                    },
                };
                int3 position = new int3(3, 3, 3);
                Assert.True(VoxelAccess.SetCell(ref table, ref pool, position, in original));

                Primitive detail = CapsuleChainEmitter.Capsule(
                    position, position, 0, 19, PrimitiveMode.SurfaceDetail, 0,
                    SurfaceStyles.MasonryJoint);
                detail.SurfaceDetail = 31;
                detail.SurfaceFlags = VoxelSurfaceFlags.IntentionalSeam;
                primitives[0] = detail;
                Rasterise(primitives, int3.zero, new int3(8),
                                              ref table, ref pool);

                VoxelCell result = VoxelAccess.GetCell(ref table, in pool, position);
                Assert.True(result.IsSolid);
                Assert.AreEqual(original.BaseMaterialId, result.BaseMaterialId);
                Assert.AreEqual(original.Surface.CoatingId, result.Surface.CoatingId);
                Assert.AreEqual(SurfaceStyles.MasonryJoint, result.Surface.StyleId);
                Assert.AreEqual(31, result.Surface.Detail);
                Assert.True((result.Surface.Flags & VoxelSurfaceFlags.IntentionalSeam) != 0);
            }
            finally
            {
                primitives.Dispose();
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void CurvedPrimitiveRasterizationPreservesAuthoredBoundaryDistance()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(128, Allocator.Temp);
            var primitives = new NativeArray<Primitive>(1, Allocator.Temp);
            try
            {
                primitives[0] = CurvedPrimitiveEmitter.Annulus(
                    new int3(12, 12, 4), 8, 5, 5, 2, false,
                    6, SurfaceStyles.Planar, PrimitiveMode.Fill, 0);
                Rasterise(primitives, int3.zero, new int3(25, 25, 9),
                                              ref table, ref pool);

                VoxelCell outerEdge = VoxelAccess.GetCell(
                    ref table, in pool, new int3(20, 12, 4));
                VoxelCell ringInterior = VoxelAccess.GetCell(
                    ref table, in pool, new int3(18, 12, 4));
                VoxelCell outside = VoxelAccess.GetCell(
                    ref table, in pool, new int3(21, 12, 4));
                Assert.True(outerEdge.IsSolid);
                Assert.True(outerEdge.Boundary.IsAuthored);
                Assert.AreEqual(8, outerEdge.Boundary.SignedQ4,
                    "the outermost included centre is half a voxel inside the authored boundary");
                Assert.Greater(ringInterior.Boundary.SignedQ4,
                               outerEdge.Boundary.SignedQ4);
                Assert.False(outside.IsSolid);
                Assert.True(outside.Boundary.IsAuthored);
                Assert.Less(outside.Boundary.SignedQ4, 0,
                    "the empty side of the isosurface must retain its signed constraint");
                Assert.AreEqual(SurfaceStyles.Planar, outerEdge.Surface.StyleId,
                    "boundary geometry and shading style remain independent values");
            }
            finally
            {
                primitives.Dispose();
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void RegionHashIncludesAuthoredBoundaryGeometry()
        {
            var aTable = new RegionTable(1, Allocator.Temp);
            var bTable = new RegionTable(1, Allocator.Temp);
            var aPool = new BrickPool(4, Allocator.Temp);
            var bPool = new BrickPool(4, Allocator.Temp);
            try
            {
                var a = new VoxelCell
                {
                    BaseMaterialId = 6,
                    Boundary = VoxelBoundarySample.FromSignedQ4(3)
                };
                var b = a;
                b.Boundary = VoxelBoundarySample.FromSignedQ4(11);
                VoxelAccess.SetCell(ref aTable, ref aPool, new int3(2, 3, 4), in a);
                VoxelAccess.SetCell(ref bTable, ref bPool, new int3(2, 3, 4), in b);
                Assert.AreNotEqual(SemanticHash(ref aTable, in aPool),
                                   SemanticHash(ref bTable, in bPool));
            }
            finally
            {
                aTable.Dispose();
                bTable.Dispose();
                aPool.Dispose();
                bPool.Dispose();
            }
        }

        [Test]
        public void RegionHashUsesLogicalCellsRatherThanPoolAllocationIndices()
        {
            var aTable = new RegionTable(1, Allocator.Temp);
            var bTable = new RegionTable(1, Allocator.Temp);
            var aPool = new BrickPool(4, Allocator.Temp);
            var bPool = new BrickPool(4, Allocator.Temp);
            try
            {
                // Shift only B's physical allocation layout.
                int unused = bPool.Allocate();
                var cell = new VoxelCell
                {
                    BaseMaterialId = 6,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = SurfaceStyles.Rounded,
                        CoatingId = Coatings.Moss,
                    },
                };
                VoxelAccess.SetCell(ref aTable, ref aPool, new int3(2, 3, 4), in cell);
                VoxelAccess.SetCell(ref bTable, ref bPool, new int3(2, 3, 4), in cell);
                Assert.True(aTable.TryGetRegion(int3.zero, out Region aRegion));
                Assert.True(bTable.TryGetRegion(int3.zero, out Region bRegion));
                Assert.AreNotEqual(aRegion.BrickRefs[0].PoolIndex,
                                   bRegion.BrickRefs[0].PoolIndex);
                Assert.AreEqual(SemanticHash(ref aTable, in aPool),
                                SemanticHash(ref bTable, in bPool));
                bPool.Free(unused);
            }
            finally
            {
                aTable.Dispose();
                bTable.Dispose();
                aPool.Dispose();
                bPool.Dispose();
            }
        }

        [Test]
        public void AuthoredCoatingsRespectBaseMaterialCompatibility()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(4, Allocator.Temp);
            try
            {
                MaterialPalette palette = default;
                palette.Register(6, 200, DestructionClass.Crumble,
                                 SurfaceStyles.Rounded, 1u << Coatings.Moss);
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, palette);
                brush.SetStyled(1, 1, 1, 6, SurfaceStyles.Rounded, Coatings.Moss);
                brush.Coat(1, 1, 1, Coatings.Snow);

                VoxelCell cell = VoxelAccess.GetCell(ref table, in pool, new int3(1));
                Assert.AreEqual(6, cell.BaseMaterialId);
                Assert.AreEqual(Coatings.Moss, cell.Surface.CoatingId,
                    "a disallowed coating must not replace an allowed one");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void MasonryWeatheringAddsMossWithoutReplacingStructuralStone()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(8, Allocator.Temp);
            try
            {
                MaterialPalette palette = default;
                palette.Register(6, 210, DestructionClass.Crumble,
                                 SurfaceStyles.Rounded, 1u << Coatings.Moss);
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, palette);
                brush.SetStyled(2, 2, 2, 6, SurfaceStyles.Rounded);

                int coated = MasonryWeathering.CoatExposedSurfaces(
                    ref brush, new int3(2), new int3(1), Coatings.Moss, 123u,
                    coverage: byte.MaxValue, dripPasses: 0);

                VoxelCell cell = VoxelAccess.GetCell(ref table, in pool, new int3(2));
                Assert.AreEqual(1, coated);
                Assert.AreEqual(6, cell.BaseMaterialId);
                Assert.AreEqual(SurfaceStyles.Rounded, cell.Surface.StyleId);
                Assert.AreEqual(Coatings.Moss, cell.Surface.CoatingId);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void UnifiedSurfaceShaderDoesNotRecognizeMaterialOrCoatingIds()
        {
            string shaderPath = Path.GetFullPath(
                "Assets/VoxelEngine/Rendering/Runtime/Shaders/SmoothSurface.shader");
            string shader = File.ReadAllText(shaderPath);

            StringAssert.DoesNotContain("material ==", shader);
            StringAssert.DoesNotContain("material !=", shader);
            StringAssert.DoesNotContain("coating ==", shader);
            StringAssert.DoesNotContain("coating !=", shader);
            StringAssert.DoesNotContain("surfaceStyle ==", shader);
            StringAssert.DoesNotContain("_StoneTexture", shader);
            StringAssert.DoesNotContain("_GrassTexture", shader);
            StringAssert.Contains("_MaterialSampling[material]", shader);
            StringAssert.Contains("_CoatingSampling[coating]", shader);
        }

        [Test]
        public void PlanarAndCurvedCellsCoexistInsideOneBrick()
        {
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(4, Allocator.Temp);
            try
            {
                var planar = new VoxelCell
                {
                    BaseMaterialId = 6,
                    Surface = new VoxelSurfaceSemantics { StyleId = SurfaceStyles.Planar }
                };
                var curved = new VoxelCell
                {
                    BaseMaterialId = 6,
                    Surface = new VoxelSurfaceSemantics { StyleId = SurfaceStyles.Rounded }
                };
                VoxelAccess.SetCell(ref table, ref pool, new int3(1, 1, 1), in planar);
                VoxelAccess.SetCell(ref table, ref pool, new int3(2, 1, 1), in curved);

                Assert.AreEqual(SurfaceStyles.Planar,
                    VoxelAccess.GetCell(ref table, in pool, new int3(1, 1, 1)).Surface.StyleId);
                Assert.AreEqual(SurfaceStyles.Rounded,
                    VoxelAccess.GetCell(ref table, in pool, new int3(2, 1, 1)).Surface.StyleId);
                VoxelAccess.Decompose(new int3(1, 1, 1), out int3 regionCoord,
                                      out int3 brickCoord, out _);
                Assert.True(table.TryGetRegion(regionCoord, out Region region));
                Assert.True(region.GetBrick(brickCoord.x, brickCoord.y, brickCoord.z).IsMixed);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void CurvedPrimitiveRasterizationIsInvariantUnderSubvolumePartitioning()
        {
            var primitives = new NativeArray<Primitive>(3, Allocator.Temp);
            primitives[0] = CurvedPrimitiveEmitter.RoundedBox(
                new int3(2, 2, 2), new int3(18, 12, 10), 3, 6,
                SurfaceStyles.Rounded, PrimitiveMode.Fill, 0);
            primitives[1] = CurvedPrimitiveEmitter.Ellipsoid(
                new int3(20, 14, 12), new int3(8, 6, 5), 6,
                SurfaceStyles.Smooth, PrimitiveMode.Fill, 1, Coatings.Moss);
            primitives[2] = CurvedPrimitiveEmitter.Annulus(
                new int3(16, 14, 16), 10, 6, 5, 2, true, 6,
                SurfaceStyles.Rounded, PrimitiveMode.Fill, 2);

            var wholeTable = new RegionTable(1, Allocator.Temp);
            var wholePool = new BrickPool(128, Allocator.Temp);
            var tiledTable = new RegionTable(1, Allocator.Temp);
            var tiledPool = new BrickPool(128, Allocator.Temp);
            try
            {
                Rasterise(primitives, int3.zero, new int3(32),
                                              ref wholeTable, ref wholePool);
                for (int z = 0; z < 32; z += 8)
                for (int y = 0; y < 32; y += 8)
                for (int x = 0; x < 32; x += 8)
                    Rasterise(primitives, new int3(x, y, z),
                        new int3(x + 8, y + 8, z + 8), ref tiledTable, ref tiledPool);

                AssertCellsEqual(ref wholeTable, in wholePool, ref tiledTable, in tiledPool,
                                 int3.zero, new int3(32));
            }
            finally
            {
                primitives.Dispose();
                wholeTable.Dispose();
                wholePool.Dispose();
                tiledTable.Dispose();
                tiledPool.Dispose();
            }
        }

        [Test]
        public void CardinalOrientationPreservesDirectionalPrimitiveMembership()
        {
            MethodInfo orient = typeof(ShapeProgram).GetMethod(
                "Orient", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(orient);
            int3 footprint = new(24, 24, 24);
            Primitive[] source =
            {
                CurvedPrimitiveEmitter.Frustum(new int3(8, 8, 4), 10, 4, 1, 2, 6,
                    SurfaceStyles.Rounded, PrimitiveMode.Fill, 0),
                CurvedPrimitiveEmitter.ArcWedge(new int3(12, 10, 12), 8, 5, 5, 2,
                    new int2(1, 0), new int2(0, 1), 6, SurfaceStyles.MasonryJoint,
                    PrimitiveMode.Fill, 1),
                PrismEmitter.Prism(new int3(2, 2, 3), new int3(10, 8, 6),
                    PrismProfile.Shed, 6, PrimitiveMode.Fill, 2)
            };

            for (byte orientation = 1; orientation < 4; orientation++)
            foreach (Primitive primitive in source)
            {
                Primitive rotated = (Primitive)orient.Invoke(
                    null, new object[] { primitive, footprint, orientation });
                for (int z = 0; z < footprint.z; z++)
                for (int y = 0; y < footprint.y; y++)
                for (int x = 0; x < footprint.x; x++)
                {
                    int3 point = new(x, y, z);
                    int3 rotatedPoint = RotatePoint(point, footprint, orientation);
                    Assert.AreEqual(Contains(in primitive, point), Contains(in rotated, rotatedPoint),
                        $"{primitive.Shape}, orientation {orientation}, voxel {point}");
                }
            }
        }

        [Test]
        public void ArchEmitsJointAwareCurvedStoneWithIndependentMoss()
        {
            var definition = new ArchFeatureDefinition
            {
                ClearSpan = 20,
                PierHeight = 18,
                RingThickness = 5,
                Depth = 6,
                VoussoirCount = 9,
                StoneMaterial = 6,
                PierStyle = SurfaceStyles.Rounded,
                RingStyle = SurfaceStyles.MasonryJoint,
                Coating = Coatings.Moss
            };
            var primitives = new NativeList<Primitive>(16, Allocator.Temp);
            try
            {
                MaterialPalette palette = default;
                palette.Register(6, 210, DestructionClass.Crumble,
                                 SurfaceStyles.Smooth, 1u << Coatings.Moss);
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                Assert.AreEqual(ArchValidationError.None,
                    definition.Validate(in palette, in surfaces, in coatings));
                Assert.True(definition.Emit(new int3(4, 0, 4), primitives));
                Assert.AreEqual(11, primitives.Length);
                Assert.AreEqual(PrimitiveShape.RoundedBox, primitives[0].Shape);
                for (int i = 2; i < primitives.Length; i++)
                {
                    Assert.AreEqual(PrimitiveShape.ArcWedge, primitives[i].Shape);
                    Assert.AreEqual(6, primitives[i].Material);
                    Assert.AreEqual(Coatings.Moss, primitives[i].Coating);
                    Assert.AreNotEqual(VoxelSurfaceFlags.None,
                        primitives[i].SurfaceFlags & VoxelSurfaceFlags.IntentionalSeam);
                }
            }
            finally
            {
                primitives.Dispose();
            }
        }

        [Test]
        public void ArchBayComposesBackingOpeningImpostsAndProudRing()
        {
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 20,
                PierHeight = 18,
                RingThickness = 5,
                Depth = 8,
                VoussoirCount = 9,
                StoneMaterial = 6,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            var bay = new ArchBayFeatureDefinition
            {
                Arch = arch,
                ShoulderWidth = 6,
                TopMargin = 4,
                FaceRecess = 2,
                PlinthHeight = 3,
                ImpostHeight = 3,
            };
            var primitives = new NativeList<Primitive>(bay.Metadata.MaxPrimitives, Allocator.Temp);
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(64, Allocator.Temp);
            try
            {
                Assert.True(bay.Emit(int3.zero, primitives));
                Assert.Greater(primitives.Length, arch.VoussoirCount + 10,
                    "the bay should include real coursed veneer blocks, not a shader-only grid");
                Assert.AreEqual(PrimitiveShape.Box, primitives[0].Shape);
                bool hasOpeningCarve = false;
                int veneerBlocks = 0;
                for (int i = 1; i < primitives.Length; i++)
                {
                    hasOpeningCarve |= primitives[i].Mode == PrimitiveMode.Carve;
                    veneerBlocks += primitives[i].Shape == PrimitiveShape.RoundedBox
                                  && primitives[i].B.z == primitives[i].A.z ? 1 : 0;
                }
                Assert.True(hasOpeningCarve);
                Assert.Greater(veneerBlocks, 8);

                RasterResult result = Rasterise(
                    primitives.AsArray(), int3.zero, bay.Metadata.Footprint,
                    ref table, ref pool);
                Assert.False(result.BudgetExceeded);

                int centreX = bay.Width / 2;
                Assert.AreEqual(Mat.Empty,
                    VoxelAccess.GetVoxel(ref table, in pool,
                        new int3(centreX, arch.PierHeight + 2, arch.Depth / 2 + 1)),
                    "the backing mass must preserve the clear arched opening");
                Assert.AreEqual(6,
                    VoxelAccess.GetVoxel(ref table, in pool,
                        new int3(centreX, arch.PierHeight + arch.OuterRadius + 2,
                                 arch.Depth / 2 + 2)),
                    "the recessed spandrel must remain above the proud ring");
            }
            finally
            {
                primitives.Dispose();
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void ArchRetainsIndividuallyJointedProfileBlocksForRendering()
        {
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 20,
                PierHeight = 18,
                RingThickness = 5,
                Depth = 8,
                VoussoirCount = 9,
                JointRecessDepth = 1,
                StoneMaterial = 6,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            var primitives = new NativeList<Primitive>(32, Allocator.Temp);
            var blocks = new ProfileBlockStore();
            try
            {
                Assert.True(arch.Emit(int3.zero, primitives, blocks));
                Assert.AreEqual(arch.VoussoirCount, blocks.Count);
                for (int i = 0; i < blocks.Count; i++)
                {
                    ProfileBlock block = blocks[i];
                    Assert.AreEqual(2, block.Axis);
                    Assert.AreEqual(arch.StoneMaterial, block.Material);
                    Assert.AreEqual(arch.RingStyle, block.SurfaceStyle);
                    Assert.Greater(block.JointHalfWidthQ4, 0);
                    Assert.Greater(block.BevelQ4, 0);
                    Assert.Greater(block.BackQ4, block.FrontQ4);
                }
            }
            finally
            {
                primitives.Dispose();
            }
        }

        [Test]
        public void SurfaceFlagsNeverChangePrimitiveMembership()
        {
            Primitive wedge = CurvedPrimitiveEmitter.ArcWedge(
                int3.zero, 10, 6, 3, 2, new int2(1, 0), new int2(0, 1),
                6, SurfaceStyles.MasonryJoint, PrimitiveMode.Fill, 0);
            int3 boundary = new int3(8, 0, 0);
            int3 interior = new int3(6, 5, 0);
            Assert.True(CurvedPrimitiveEmitter.Contains(in wedge, boundary));
            Assert.True(CurvedPrimitiveEmitter.Contains(in wedge, interior));

            wedge.SurfaceFlags = VoxelSurfaceFlags.IntentionalSeam;
            Assert.True(CurvedPrimitiveEmitter.Contains(in wedge, boundary),
                "join metadata must not secretly carve structural geometry");
            Assert.True(CurvedPrimitiveEmitter.Contains(in wedge, new int3(6, 0, 0)),
                "a recessed joint requires an explicit carve or boundary constraint");
            Assert.True(CurvedPrimitiveEmitter.Contains(in wedge, interior));
        }

        [Test]
        public void ArchValidationReportsStructuralAndCatalogueErrors()
        {
            var definition = new ArchFeatureDefinition
            {
                ClearSpan = 5,
                PierHeight = 0,
                RingThickness = 0,
                Depth = 0,
                VoussoirCount = 33,
                StoneMaterial = 31,
                PierStyle = 29,
                RingStyle = 30,
                Coating = Coatings.Moss
            };
            MaterialPalette palette = default;
            SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
            CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
            ArchValidationError errors = definition.Validate(
                in palette, in surfaces, in coatings);
            Assert.True((errors & ArchValidationError.InvalidClearSpan) != 0);
            Assert.True((errors & ArchValidationError.InvalidPierHeight) != 0);
            Assert.True((errors & ArchValidationError.InvalidRingThickness) != 0);
            Assert.True((errors & ArchValidationError.InvalidDepth) != 0);
            Assert.True((errors & ArchValidationError.InvalidVoussoirCount) != 0);
            Assert.True((errors & ArchValidationError.UnknownStoneMaterial) != 0);
            Assert.True((errors & ArchValidationError.UnknownPierStyle) != 0);
            Assert.True((errors & ArchValidationError.UnknownRingStyle) != 0);

            palette.Register(6, 210, DestructionClass.Crumble,
                             SurfaceStyles.Smooth, 0u);
            definition = new ArchFeatureDefinition
            {
                ClearSpan = 20,
                PierHeight = 18,
                RingThickness = 5,
                Depth = 6,
                VoussoirCount = 9,
                StoneMaterial = 6,
                PierStyle = SurfaceStyles.Rounded,
                RingStyle = SurfaceStyles.MasonryJoint,
                Coating = Coatings.Moss
            };
            errors = definition.Validate(in palette, in surfaces, in coatings);
            Assert.True((errors & ArchValidationError.DisallowedCoating) != 0);
        }

        [Test]
        public void JournalConsumersHaveIndependentCursorsAndDetectOverflow()
        {
            var journal = new VoxelChangeJournal(2);
            journal.PublishRegion(int3.zero, VoxelChangeKind.Occupancy);
            ulong firstConsumer = 0;
            var records = new System.Collections.Generic.List<VoxelChangeRecord>();
            Assert.True(journal.ReadSince(ref firstConsumer, records));
            Assert.AreEqual(1, records.Count);

            ulong slowConsumer = 0;
            journal.PublishRegion(new int3(1, 0, 0), VoxelChangeKind.Coating);
            journal.PublishRegion(new int3(2, 0, 0), VoxelChangeKind.SurfaceStyle);
            Assert.False(journal.ReadSince(ref slowConsumer, records));
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(3ul, slowConsumer);

            Assert.True(journal.ReadSince(ref firstConsumer, records));
            Assert.AreEqual(2, records.Count);
        }

        [Test]
        public void BoundedJournalReadsAdvanceIncrementallyAndPreserveOverflowSignal()
        {
            var journal = new VoxelChangeJournal(8);
            for (int i = 0; i < 5; i++)
                journal.PublishRegion(new int3(i, 0, 0), VoxelChangeKind.Occupancy);

            ulong cursor = 0;
            var records = new System.Collections.Generic.List<VoxelChangeRecord>();
            Assert.True(journal.ReadSince(ref cursor, records, 2, out bool hasMore));
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(2ul, cursor);
            Assert.True(hasMore);

            Assert.True(journal.ReadSince(ref cursor, records, 2, out hasMore));
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(4ul, cursor);
            Assert.True(hasMore);

            Assert.True(journal.ReadSince(ref cursor, records, 2, out hasMore));
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(5ul, cursor);
            Assert.False(hasMore);

            var tiny = new VoxelChangeJournal(2);
            tiny.PublishRegion(int3.zero);
            tiny.PublishRegion(new int3(1, 0, 0));
            tiny.PublishRegion(new int3(2, 0, 0));
            ulong stale = 0;
            Assert.False(tiny.ReadSince(ref stale, records, 1, out hasMore));
            Assert.AreEqual(0, records.Count,
                "A consumer that lost exact history should recover state, not copy unusable replay data.");
            Assert.AreEqual(tiny.CurrentVersion, stale);
            Assert.False(hasMore);
        }

        [Test]
        public void SolidInvalidationIsBoundedToChangedChunkAndRequiredHalo()
        {
            using var cache = new CpuTransvoxelChunkCache();

            // Render residency is admitted only inside the camera clipmap window: surface
            // discovery can cover a far larger resident Storage window than a ring draws, and
            // without this bound _known and the build queue would grow with world streaming
            // rather than the fixed view footprint. A cache with no window admits nothing, so
            // the window has to be established before invalidation means anything.
            cache.SetClipmapWindow(int3.zero, 2);

            cache.InvalidateSurfaceBricks(new[] { new int3(1, 1, 1) });
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount,
                "A cold mutation advances truth without creating offscreen render work.");
            Assert.True(cache.RequestHierarchyCoverage(
                int3.zero, SurfaceBuildPriority.VisibleRefinement));
            Assert.AreEqual(1, cache.DirtyCount);

            // Brick eight begins the next 64-voxel extraction chunk. Because it lies on all three
            // local zero faces, the one-brick sampling halo admits seven neighbours too. Those
            // invalidations stay cold: only the already-requested origin remains queued.
            cache.InvalidateSurfaceBricks(new[] { new int3(8, 8, 8) });
            Assert.AreEqual(8, cache.KnownCount);
            Assert.AreEqual(1, cache.DirtyCount,
                "Halo invalidation must not flood newly admitted cold neighbours into the queue.");
        }

        [Test]
        public void SolidWorkersPartitionEveryChunkExactlyOnce()
        {
            var owners = new int[5, 5, 5];
            var workers = new CpuTransvoxelChunkCache[VoxelSurfaceScheduler.SolidWorkerCount];
            try
            {
                for (int worker = 0; worker < workers.Length; worker++)
                    workers[worker] = new CpuTransvoxelChunkCache
                    {
                        ShardIndex = worker, ShardCount = workers.Length
                    };
                var bricks = new[]
                {
                    new int3(-8, -8, -8), new int3(0, 0, 0), new int3(4, 4, 4),
                    new int3(9, 3, -5), new int3(16, 16, 16),
                };
                for (int b = 0; b < bricks.Length; b++)
                    for (int worker = 0; worker < workers.Length; worker++)
                        workers[worker].InvalidateSurfaceBricks(new[] { bricks[b] });

                int total = 0;
                for (int worker = 0; worker < workers.Length; worker++)
                    total += workers[worker].KnownCount;
                using var reference = new CpuTransvoxelChunkCache();
                for (int b = 0; b < bricks.Length; b++)
                    reference.InvalidateSurfaceBricks(new[] { bricks[b] });
                Assert.AreEqual(reference.KnownCount, total,
                    "sharding must neither duplicate nor lose extraction chunks");
            }
            finally
            {
                for (int i = 0; i < workers.Length; i++) workers[i]?.Dispose();
            }
        }

        [Test]
        public void TimingWindowReportsOrderedRollingPercentilesWithoutGrowing()
        {
            var timing = new VoxelTimingWindow();
            for (int i = 1; i <= 256; i++) timing.Add(i);

            VoxelTimingSummary summary = timing.Snapshot();
            Assert.AreEqual(256ul, summary.SampleCount);
            Assert.AreEqual(256.0, summary.LastMs);
            Assert.AreEqual(192.0, summary.P50Ms,
                "the fixed 128-sample window should have discarded samples 1 through 128");
            Assert.AreEqual(250.0, summary.P95Ms);
            Assert.AreEqual(256.0, summary.MaxMs);
        }

        [Test]
        public void TimingWindowClampsInvalidNegativeDurationsAndIgnoresNonFiniteValues()
        {
            var timing = new VoxelTimingWindow();
            timing.Add(-4.0);
            timing.Add(double.NaN);
            timing.Add(double.PositiveInfinity);

            VoxelTimingSummary summary = timing.Snapshot();
            Assert.AreEqual(1ul, summary.SampleCount);
            Assert.AreEqual(0.0, summary.LastMs);
            Assert.AreEqual(0.0, summary.P95Ms);
        }

        [Test]
        public void RegionInvalidationOnlyTouchesOwnerAndSamplingHalo()
        {
            Assert.True(CpuTransvoxelChunkCache.ChunkOverlapsRegion(
                new int3(0, 0, 0), int3.zero));
            Assert.True(CpuTransvoxelChunkCache.ChunkOverlapsRegion(
                new int3(7, 0, 0), new int3(1, 0, 0)),
                "the last chunk before a region boundary samples its neighbour");
            Assert.False(CpuTransvoxelChunkCache.ChunkOverlapsRegion(
                new int3(0, 0, 0), new int3(0, 0, 1)),
                "an entire neighbouring region must not be invalidated for one-sample padding");
            Assert.False(CpuTransvoxelChunkCache.ChunkOverlapsRegion(
                new int3(32, 0, 0), int3.zero));
        }

        [Test]
        public void ProfileBlocksAreIndexedOnlyIntoIntersectingChunks()
        {
            using var cache = new CpuTransvoxelChunkCache();
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(1, Allocator.Temp);
            var cameraObject = new UnityEngine.GameObject("profile-index-test-camera");
            var store = new ProfileBlockStore();
            try
            {
                store.Add(new ProfileBlock
                {
                    Centre = new int3(16, 16, 16), InnerRadiusQ4 = 32,
                    OuterRadiusQ4 = 64, FrontQ4 = 0, BackQ4 = 64,
                    StartDirection = new int2(1, 0), EndDirection = new int2(0, 1),
                    Axis = 2, Material = 6, SurfaceStyle = SurfaceStyles.MasonryJoint,
                });
                MaterialPalette palette = default;
                palette.Register(6, 200, DestructionClass.Crumble,
                                 SurfaceStyles.MasonryJoint, uint.MaxValue);
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                MaterialPaletteView paletteView = palette;
                SurfaceCatalogueView surfaceView = surfaces;
                CoatingCatalogueView coatingView = coatings;
                cache.Prepare(new RegionReadSource(in table, in pool), in paletteView, in surfaceView, in coatingView, store,
                              cameraObject.AddComponent<UnityEngine.Camera>(), 0.1f, 0, 0.0);
                Assert.AreEqual(1, cache.IndexedProfileBlockCount(int3.zero));
                Assert.AreEqual(0, cache.IndexedProfileBlockCount(new int3(8, 0, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void ProductionSolidExtractorAcceptsWorldOwnedSurfaceRules()
        {
            using var cache = new CpuTransvoxelChunkCache();
            var table = new RegionTable(1, Allocator.Temp);
            var pool = new BrickPool(1, Allocator.Temp);
            var cameraObject = new UnityEngine.GameObject("catalogue-input-test-camera");
            try
            {
                MaterialPalette palette = default;
                palette.Register(1, 200, DestructionClass.Crumble,
                                 SurfaceStyles.Smooth, uint.MaxValue);
                SurfaceCatalogue custom = SurfaceCatalogue.CreateBuiltIns();
                SurfaceJoinRule join = custom.GetJoin(1, 1);
                join.BlendWidth = 7;
                custom.SetJoin(1, 1, in join);
                custom.Seal(41, custom.ComputeHash());
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                Assert.AreNotEqual(cache.ActiveSurfaceCatalogueHash, custom.CatalogueHash);
                MaterialPaletteView paletteView = palette;
                SurfaceCatalogueView surfaceView = custom;
                CoatingCatalogueView coatingView = coatings;

                cache.Prepare(new RegionReadSource(in table, in pool), in paletteView, in surfaceView, in coatingView, null,
                              cameraObject.AddComponent<UnityEngine.Camera>(), 0.1f, 0, 0.0);

                Assert.AreEqual(custom.CatalogueHash, cache.ActiveSurfaceCatalogueHash);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void WorldTeardownReleasesRendererBorrowsBeforeStorageBindingIsCleared()
        {
            string composition = File.ReadAllText(
                "Assets/VoxelEngine/Composition/RenderingComposition.cs");
            int release = composition.IndexOf("VoxelRenderBridge.ReleaseWorldResources();",
                                              System.StringComparison.Ordinal);
            int clear = composition.IndexOf("s_hasWorld = false;",
                                            System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(release, 0,
                "world teardown must synchronously release renderer Storage borrows");
            Assert.Greater(clear, release,
                "renderer Storage borrows must be released before the world binding is cleared");

            string pass = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs");
            StringAssert.Contains("RegisterWorldReleaseHandler(ReleaseWorldResources)", pass);
            StringAssert.Contains("UnregisterWorldReleaseHandler(ReleaseWorldResources)", pass);
            StringAssert.Contains("_scheduler.Dispose();", pass,
                "world release must synchronously drain jobs/pins, not abandon the scheduler");
        }

        private static void AssertCellsEqual(ref RegionTable aTable, in BrickPool aPool,
                                             ref RegionTable bTable, in BrickPool bPool,
                                             int3 min, int3 max)
        {
            for (int z = min.z; z < max.z; z++)
            for (int y = min.y; y < max.y; y++)
            for (int x = min.x; x < max.x; x++)
            {
                int3 p = new(x, y, z);
                Assert.True(VoxelAccess.GetCell(ref aTable, in aPool, p)
                    .Equals(VoxelAccess.GetCell(ref bTable, in bPool, p)), $"cell mismatch at {p}");
            }
        }

        private static bool Contains(in Primitive primitive, int3 voxel) => primitive.Shape switch
        {
            PrimitiveShape.Prism => PrismEmitter.Contains(in primitive, voxel),
            PrimitiveShape.Frustum or PrimitiveShape.ArcWedge =>
                CurvedPrimitiveEmitter.Contains(in primitive, voxel),
            _ => false
        };

        private static int3 RotatePoint(int3 point, int3 footprint, byte orientation) =>
            orientation switch
            {
                1 => new int3(footprint.z - 1 - point.z, point.y, point.x),
                2 => new int3(footprint.x - 1 - point.x, point.y,
                              footprint.z - 1 - point.z),
                3 => new int3(point.z, point.y, footprint.x - 1 - point.x),
                _ => point
            };

        private static uint SemanticHash(ref RegionTable table, in BrickPool pool)
        {
            var source = new RegionReadSource(in table, in pool);
            Assert.AreEqual(
                RegionSnapshotCaptureResult.Ok,
                source.CaptureSemanticSnapshot(
                    int3.zero,
                    RegionSemanticSnapshotLimits.DefaultMaxSnapshotBytes,
                    out RegionSemanticSnapshot snapshot));
            return snapshot.SemanticHash;
        }

        private static RasterResult Rasterise(
            NativeArray<Primitive> primitives,
            int3 min,
            int3 max,
            ref RegionTable table,
            ref BrickPool pool,
            bool markHardSurface = false)
        {
            var reads = new RegionReadSource(in table, in pool);
            var mutations = new RegionMutationStore(in table, in pool);
            return PrimitiveRasteriser.Rasterise(
                primitives, min, max, reads, mutations, markHardSurface);
        }

    }
}