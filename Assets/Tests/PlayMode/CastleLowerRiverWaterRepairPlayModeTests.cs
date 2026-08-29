using System.Collections;
using System.Reflection;
using Game.Materials.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Composition.Api;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using GameCastleLayout = Game.Structures.Api.CastleLayout;
using GameCastlePlan = Game.Structures.Api.CastlePlan;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CastleLowerRiverWaterRepairPlayModeTests
    {
        private const uint ShowcaseSeed = 0x5EED1234u;

        [Test]
        public void ShowcaseMarkedReceivingBankBecomesWaterAndStopsBeforeOuterShore()
        {
            GameCastlePlan plan = CastlePlanner.Plan(new int3(256, 50, 376), ShowcaseSeed);
            int top = plan.Centre.y + plan.PlateauHeight;
            int riverY = top - GameCastleLayout.LowerRiverDepth;
            int streamX = GameCastleLayout.WaterfallStreamX(in plan);
            int channelZ = GameCastleLayout.LowerRiverZAt(in plan, streamX);
            int[] markedOffsets = { 39, 50, 55, 67, 79 };
            const int dryOuterShoreOffset = 84;
            const int cascadeOffset = 50;

            using IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 16,
                mixedBrickCapacity: 4096,
                changeJournalCapacity: 128);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(storage, 1_000_000);

            foreach (int offset in markedOffsets)
                SeedGrassShelf(authoring, streamX, riverY, channelZ + offset);
            SeedGrassShelf(authoring, streamX, riverY, channelZ + dryOuterShoreOffset);
            authoring.Set(
                streamX + 1,
                riverY + 2,
                channelZ + cascadeOffset,
                GameMaterialIds.Cascade);

            CastleLowerRiverWaterRepair.Repair(authoring, in plan);

            foreach (int offset in markedOffsets)
            {
                AssertMaterial(
                    storage.SurfaceQuery,
                    new int3(streamX, riverY, channelZ + offset),
                    GameMaterialIds.Water,
                    $"marked receiving-bank offset +{offset} must be water");
                AssertMaterial(
                    storage.SurfaceQuery,
                    new int3(streamX, riverY + 2, channelZ + offset),
                    GameMaterialIds.Empty,
                    $"grass shelf above marked offset +{offset} must be removed");
            }

            AssertMaterial(
                storage.SurfaceQuery,
                new int3(streamX, riverY + 2, channelZ + dryOuterShoreOffset),
                GameMaterialIds.Grass,
                "bounded repair must stop before the dry outer shore");
            AssertMaterial(
                storage.SurfaceQuery,
                new int3(streamX + 1, riverY + 2, channelZ + cascadeOffset),
                GameMaterialIds.Cascade,
                "waterfall cascade material must survive the receiving-bank repair");

            Assert.That(authoring.TotalVoxelsWritten, Is.LessThan(250_000),
                "repair must remain a bounded startup/content-authoring operation");
        }

        [Test]
        public void FarFieldRecapturePreservesWaterMaterialForRenderer()
        {
            using IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 2,
                mixedBrickCapacity: 4096,
                changeJournalCapacity: 32);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(storage, 64);
            var farField = new FarFieldStructureStore();

            var sample = new int3(16, 100, 16);
            authoring.Set(sample.x, sample.y, sample.z, GameMaterialIds.Grass);
            farField.CaptureRegion(int3.zero, storage.Reads, ShowcaseSeed);

            Assert.AreEqual(100, farField.AuthoredTerrainHeightAt(sample.x, sample.z));
            Assert.AreEqual(
                GameMaterialIds.Grass,
                farField.AuthoredTerrainMaterialAt(sample.x, sample.z));
            int grassVersion = farField.Version;

            authoring.Set(sample.x, sample.y, sample.z, GameMaterialIds.Water);
            farField.CaptureRegion(int3.zero, storage.Reads, ShowcaseSeed);

            Assert.AreEqual(100, farField.AuthoredTerrainHeightAt(sample.x, sample.z));
            Assert.AreEqual(
                GameMaterialIds.Water,
                farField.AuthoredTerrainMaterialAt(sample.x, sample.z));
            Assert.Greater(farField.Version, grassVersion,
                "material-only far-field changes must invalidate cached presentation");

            ShowcaseMaterialSet materialRoles = TestMaterialRoles();
            byte renderedMaterial = VoxelFarTerrain.ResolveFarSurfaceMaterial(
                materialRoles,
                isStructure: false,
                hasAuthoredTerrain: true,
                authoredTerrainMaterial: farField.AuthoredTerrainMaterialAt(sample.x, sample.z),
                height: farField.AuthoredTerrainHeightAt(sample.x, sample.z));
            Assert.AreEqual(GameMaterialIds.Water, renderedMaterial,
                "far terrain must render the retained authored water material, not generic grass");

            byte analyticMaterial = VoxelFarTerrain.ResolveFarSurfaceMaterial(
                materialRoles,
                isStructure: false,
                hasAuthoredTerrain: false,
                authoredTerrainMaterial: GameMaterialIds.Empty,
                height: ShowcaseWorld.BaseHeightVoxels);
            Assert.AreEqual(
                materialRoles.SurfaceAt(
                    ShowcaseWorld.BaseHeightVoxels, ShowcaseWorld.BaseHeightVoxels),
                analyticMaterial,
                "ordinary analytic terrain must retain the existing terrain-role path");
        }

        [UnityTest]
        public IEnumerator StartupFallbackLeavesSynchronousCriticalFootprintUncovered()
        {
            var cameraObject = new GameObject("Agent7FallbackRegressionCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(59.4500465f, 20.3501129f, -1.54625964f);

            VoxelFarTerrain far = VoxelFarTerrain.Create(
                parent: null,
                seed: ShowcaseSeed,
                innerRadiusMetres: 220f,
                outerRadiusMetres: 4000f);

            yield return null;

            int fallbackRing = far.RingCount - 1;
            Mesh fallback = RuntimeRingMesh(far, fallbackRing);
            Assert.IsNotNull(fallback, "startup fallback mesh must exist before outer sampling lands");

            Vector2 cameraXZ = new(camera.transform.position.x, camera.transform.position.z);
            Assert.IsFalse(
                MeshCoversPointXZ(fallback, cameraXZ),
                "startup fallback must never cover the synchronously sampled critical footprint");

            Vector2 horizonProbe = cameraXZ + new Vector2(1000f, 0f);
            Assert.IsTrue(
                MeshCoversPointXZ(fallback, horizonProbe),
                "startup fallback must retain bounded horizon coverage outside ring zero");
            Assert.That(fallback.vertexCount, Is.GreaterThan(8).And.LessThan(3000),
                "semantic fallback must add enough samples to follow terrain without approaching full outer-ring density");
            Assert.That(fallback.triangles.Length, Is.LessThan(12000),
                "semantic fallback topology must remain a bounded startup cost");

            Object.Destroy(far.gameObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupFallbackPreservesAuthoredWaterHeightAndMaterial()
        {
            using IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 2,
                mixedBrickCapacity: 4096,
                changeJournalCapacity: 32);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(storage, 512);
            var farField = new FarFieldStructureStore();

            // This 51.2 m region sits immediately outside the issue camera's synchronous ring-zero
            // north edge. Fill every far-field coarse sample with lowered water so at least one
            // production fallback vertex must consume authored semantics rather than analytic grass.
            const int regionX = 1;
            const int regionZ = 6;
            const int waterY = 100;
            int originX = regionX * 512;
            int originZ = regionZ * 512;
            for (int z = 16; z < 512; z += 32)
            for (int x = 16; x < 512; x += 32)
                authoring.Set(originX + x, waterY, originZ + z, GameMaterialIds.Water);
            farField.CaptureRegion(new int3(regionX, 0, regionZ), storage.Reads, ShowcaseSeed);

            var cameraObject = new GameObject("Agent7SemanticFallbackCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(59.4500465f, 20.3501129f, -1.54625964f);

            VoxelFarTerrain far = VoxelFarTerrain.Create(
                parent: null,
                seed: ShowcaseSeed,
                innerRadiusMetres: 220f,
                outerRadiusMetres: 4000f);
            far.Structures = farField;
            far.MaterialRoles = TestMaterialRoles();

            yield return null;

            Mesh fallback = RuntimeRingMesh(far, far.RingCount - 1);
            Vector3[] vertices = fallback.vertices;
            Color[] colours = fallback.colors;
            Vector4 waterAlbedo = RenderingComposition.GetMaterialAlbedo(GameMaterialIds.Water);
            bool foundAuthoredWaterVertex = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                if (vertex.x < 51.2f || vertex.x > 102.4f
                    || vertex.z < 307.2f || vertex.z > 358.4f)
                    continue;

                foundAuthoredWaterVertex = true;
                Assert.That(vertex.y, Is.EqualTo(waterY * 0.1f).Within(0.01f),
                    "startup fallback must use authored lowered water height at issue-scale horizon samples");
                Assert.That(colours[i].r, Is.EqualTo(waterAlbedo.x).Within(0.01f));
                Assert.That(colours[i].g, Is.EqualTo(waterAlbedo.y).Within(0.01f));
                Assert.That(colours[i].b, Is.EqualTo(waterAlbedo.z).Within(0.01f),
                    "startup fallback must preserve authored water albedo instead of generic terrain grass");
                break;
            }

            Assert.IsTrue(foundAuthoredWaterVertex,
                "bounded fallback sampling must include the first unresolved band containing the authored water probe");
            Assert.That(fallback.vertexCount, Is.LessThan(3000),
                "water-aware fallback must remain well below full unresolved clipmap density");

            Object.Destroy(far.gameObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupFallbackRecentersAfterCameraRelocationBeforeRingPublication()
        {
            var cameraObject = new GameObject("Agent7FallbackRelocationCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(59.4500465f, 20.3501129f, -1.54625964f);

            VoxelFarTerrain far = VoxelFarTerrain.Create(
                parent: null,
                seed: ShowcaseSeed,
                innerRadiusMetres: 220f,
                outerRadiusMetres: 4000f);

            yield return null;
            int fallbackRing = far.RingCount - 1;
            Mesh fallback = RuntimeRingMesh(far, fallbackRing);
            Vector2 originalCameraXZ = new(camera.transform.position.x, camera.transform.position.z);
            Assert.IsFalse(MeshCoversPointXZ(fallback, originalCameraXZ),
                "initial startup camera must begin inside the fallback exclusion");

            CompleteAndForgetScheduledHeightJob(far, expectedRing: 1);
            camera.transform.position += new Vector3(900f, 0f, 0f);
            Vector2 relocatedCameraXZ = new(camera.transform.position.x, camera.transform.position.z);
            yield return null;

            fallback = RuntimeRingMesh(far, fallbackRing);
            Assert.That(fallback.bounds.center.x,
                Is.EqualTo(camera.transform.position.x).Within(0.01f),
                "startup fallback bounds must follow a relocated camera before another ring publishes");
            Assert.That(fallback.bounds.center.z,
                Is.EqualTo(camera.transform.position.z).Within(0.01f),
                "startup fallback bounds must follow a relocated camera before another ring publishes");
            Assert.IsFalse(MeshCoversPointXZ(fallback, relocatedCameraXZ),
                "emergency fallback must never move underneath the current startup camera");
            Assert.IsFalse(MeshCoversPointXZ(fallback, originalCameraXZ),
                "recentering must preserve already-published critical-ring ownership");
            Assert.IsTrue(
                MeshCoversPointXZ(fallback, relocatedCameraXZ + new Vector2(1000f, 0f)),
                "recentering must retain unresolved horizon coverage beyond the current critical footprint");

            Object.Destroy(far.gameObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupFallbackDoesNotOverlapPublishedFirstOuterRing()
        {
            var cameraObject = new GameObject("Agent7FallbackHandoffReproCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(59.4500465f, 20.3501129f, -1.54625964f);

            VoxelFarTerrain far = VoxelFarTerrain.Create(
                parent: null,
                seed: ShowcaseSeed,
                innerRadiusMetres: 220f,
                outerRadiusMetres: 4000f);

            yield return null;
            CompleteScheduledHeightJob(far, expectedRing: 1);
            const int publicationFrameBudget = 3;
            for (int frame = 0;
                 frame < publicationFrameBudget && !IsRingHeightPublished(far, ring: 1);
                 frame++)
                yield return null;

            Assert.IsTrue(IsRingHeightPublished(far, ring: 1),
                "a completed first outer-ring job must publish through production LateUpdate within three player-loop turns");
            Mesh firstOuter = RuntimeRingMesh(far, ring: 1);
            Assert.IsNotNull(firstOuter, "first asynchronous outer ring must publish during startup");
            Assert.Greater(firstOuter.triangles.Length, 0,
                "first asynchronous outer ring must carry authoritative topology");

            int fallbackRing = far.RingCount - 1;
            Mesh fallback = RuntimeRingMesh(far, fallbackRing);
            Assert.IsNotNull(fallback,
                "startup fallback must still cover unresolved rings when ring one publishes");

            Vector2 probe = new(camera.transform.position.x + 350f, camera.transform.position.z);
            Assert.IsTrue(MeshCoversPointXZ(firstOuter, probe),
                "minimal repro probe must lie inside the published ring-one annulus");
            Assert.IsFalse(MeshCoversPointXZ(fallback, probe),
                "startup fallback must relinquish any XZ footprint as soon as an authoritative outer ring publishes there");

            Object.Destroy(far.gameObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        private static void CompleteScheduledHeightJob(VoxelFarTerrain far, int expectedRing)
        {
            const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo scheduledField = typeof(VoxelFarTerrain).GetField(
                "_heightJobScheduled", PrivateInstance);
            FieldInfo ringField = typeof(VoxelFarTerrain).GetField(
                "_heightJobRing", PrivateInstance);
            FieldInfo handleField = typeof(VoxelFarTerrain).GetField(
                "_heightJobHandle", PrivateInstance);

            Assert.IsNotNull(scheduledField, "height-job scheduled field must remain discoverable");
            Assert.IsNotNull(ringField, "height-job ring field must remain discoverable");
            Assert.IsNotNull(handleField, "height-job handle field must remain discoverable");
            Assert.IsTrue((bool)scheduledField.GetValue(far),
                "first outer-ring sampling job must be scheduled after the initial LateUpdate");
            Assert.AreEqual(expectedRing, (int)ringField.GetValue(far),
                "deterministic handoff regression must complete the first outer ring, not another ring");

            JobHandle handle = (JobHandle)handleField.GetValue(far);
            handle.Complete();
        }

        private static void CompleteAndForgetScheduledHeightJob(
            VoxelFarTerrain far, int expectedRing)
        {
            const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo scheduledField = typeof(VoxelFarTerrain).GetField(
                "_heightJobScheduled", PrivateInstance);
            FieldInfo ringField = typeof(VoxelFarTerrain).GetField(
                "_heightJobRing", PrivateInstance);
            FieldInfo handleField = typeof(VoxelFarTerrain).GetField(
                "_heightJobHandle", PrivateInstance);

            Assert.IsNotNull(scheduledField, "height-job scheduled field must remain discoverable");
            Assert.IsNotNull(ringField, "height-job ring field must remain discoverable");
            Assert.IsNotNull(handleField, "height-job handle field must remain discoverable");
            Assert.IsTrue((bool)scheduledField.GetValue(far),
                "startup relocation discriminator requires an outstanding outer-ring job");
            Assert.AreEqual(expectedRing, (int)ringField.GetValue(far),
                "startup relocation discriminator must quiesce ring one");

            JobHandle handle = (JobHandle)handleField.GetValue(far);
            handle.Complete();
            scheduledField.SetValue(far, false);
            ringField.SetValue(far, -1);
        }

        private static bool IsRingHeightPublished(VoxelFarTerrain far, int ring)
        {
            const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo validField = typeof(VoxelFarTerrain).GetField(
                "_ringHeightValid", PrivateInstance);
            Assert.IsNotNull(validField, "ring publication state must remain discoverable");
            var published = (IList)validField.GetValue(far);
            Assert.That(ring, Is.InRange(0, published.Count - 1),
                "requested ring must exist before publication is inspected");
            return (bool)published[ring];
        }

        private static Mesh RuntimeRingMesh(VoxelFarTerrain far, int ring)
        {
            const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo meshesField = typeof(VoxelFarTerrain).GetField(
                "_ringMeshes", PrivateInstance);
            Assert.IsNotNull(meshesField, "runtime ring mesh list must remain discoverable");
            var meshes = (IList)meshesField.GetValue(far);
            Assert.That(ring, Is.InRange(0, meshes.Count - 1),
                "requested runtime ring must exist");
            return (Mesh)meshes[ring];
        }

        private static bool MeshCoversPointXZ(Mesh mesh, Vector2 point)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a3 = vertices[triangles[i]];
                Vector3 b3 = vertices[triangles[i + 1]];
                Vector3 c3 = vertices[triangles[i + 2]];
                var a = new Vector2(a3.x, a3.z);
                var b = new Vector2(b3.x, b3.z);
                var c = new Vector2(c3.x, c3.z);
                if (PointInTriangle(point, a, b, c)) return true;
            }

            return false;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = SignedArea(point, a, b);
            float d2 = SignedArea(point, b, c);
            float d3 = SignedArea(point, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float SignedArea(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.x - p3.x) * (p2.y - p3.y)
            - (p2.x - p3.x) * (p1.y - p3.y);

        private static ShowcaseMaterialSet TestMaterialRoles()
        {
            return new ShowcaseMaterialSet(
                terrainDeep: GameMaterialIds.Bedrock,
                terrainSubsurface: GameMaterialIds.Dirt,
                terrainLowSurface: GameMaterialIds.Grass,
                terrainHighSurface: GameMaterialIds.Stone,
                gate: GameMaterialIds.Wood,
                referenceArch: GameMaterialIds.MasonryMedium,
                farStructure: GameMaterialIds.Stone,
                worldgenFoundation: GameMaterialIds.Stone,
                worldgenMasonry: GameMaterialIds.MasonryMedium,
                worldgenDarkMasonry: GameMaterialIds.DarkStone,
                worldgenTimber: GameMaterialIds.Wood,
                worldgenGlass: GameMaterialIds.Glass,
                worldgenWarmWindow: GameMaterialIds.LitWindow,
                worldgenRoofTile: GameMaterialIds.Tile,
                worldgenSlate: GameMaterialIds.Slate,
                worldgenCloth: GameMaterialIds.Cloth,
                worldgenMoss: GameMaterialIds.Moss,
                worldgenWater: GameMaterialIds.Water,
                worldgenRoadSurface: GameMaterialIds.MasonrySmall,
                structuralMask: 0u);
        }

        private static void SeedGrassShelf(
            IStructureAuthoringSession authoring,
            int x,
            int riverY,
            int z)
        {
            authoring.FillColumnBulk(
                x,
                riverY - 8,
                riverY + 2,
                z,
                GameMaterialIds.Dirt);
            authoring.Set(x, riverY + 2, z, GameMaterialIds.Grass);
        }

        private static void AssertMaterial(
            IVoxelSurfaceQuery query,
            int3 position,
            byte expected,
            string message)
        {
            if (expected == GameMaterialIds.Empty)
            {
                if (!query.TryRead(position, out VoxelCell emptyCell))
                    return;
                Assert.AreEqual(expected, emptyCell.BaseMaterialId, message);
                return;
            }

            Assert.IsTrue(query.TryRead(position, out VoxelCell cell), message);
            Assert.AreEqual(expected, cell.BaseMaterialId, message);
        }
    }
}
