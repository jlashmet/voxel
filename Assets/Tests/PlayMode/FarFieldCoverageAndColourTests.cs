using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Core.Terrain;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Two distinct far-field defects, reported from play and reproduced here.
    ///
    /// <para><b>1. The distant range is pure grey.</b> <c>VoxelFarTerrain</c> shares
    /// <see cref="TerrainSampler.HeightAt"/> with the voxel world but shares nothing else. It
    /// writes positions, triangles, and normals into its ring meshes and never writes a colour;
    /// its material is a single flat <c>_BaseColor</c>. So every distant mountain is the same
    /// desaturated grey regardless of altitude, slope, or the material the voxel world would
    /// actually place there. The near field shades from the material palette, the far field does
    /// not, and they meet at the streaming radius.</para>
    ///
    /// <para><b>2. Ground arrives late inside the clipmap hole.</b> Ring 0 punches a hole of
    /// <c>InnerRadiusMetres</c> so it does not z-fight the voxel world. That radius is fixed once
    /// in <c>OnEnable</c> and is blind to what is actually resident. Voxel regions stream in over
    /// seconds on a 3 ms/frame budget, so for the whole of that time the hole contains neither
    /// far mesh nor voxels — it contains nothing, and the player watches it fill in around them.
    /// The far mesh is supposed to be the lower LOD that covers exactly this gap.</para>
    ///
    /// <para>A third defect falls out of the second: the hole is a Chebyshev square while voxel
    /// residency is a Euclidean disc, so the two do not nest and the mismatch never closes.</para>
    ///
    /// These are the same class as the raymarch/smooth-surface drift: two renderers describing
    /// one world, agreeing on geometry and silently disagreeing on everything else.
    /// </summary>
    public sealed class FarFieldCoverageAndColourTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        private static IEnumerator LoadShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            for (int i = 0; i < 8; i++) yield return null;
        }

        private static VoxelShowcase Showcase() => Object.FindFirstObjectByType<VoxelShowcase>();

        private static VoxelFarTerrain Far() => Object.FindFirstObjectByType<VoxelFarTerrain>();

        private static ShowcaseWorld World(VoxelShowcase showcase) =>
            (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);

        private static List<Mesh> RingMeshes(VoxelFarTerrain far) =>
            (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

        private static int LoadRadiusRegions(VoxelShowcase showcase) =>
            (int)typeof(VoxelShowcase)
                .GetField("m_LoadRadiusRegions", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);

        // ---------------------------------------------------------------------
        // 1. Colour
        // ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FarTerrainMeshCarriesPerVertexMaterialColour()
        {
            // The narrowest statement of the grey-mountain bug. Height alone cannot be shaded
            // into anything but one flat colour; the mesh has to carry what the ground is made
            // of, not just where it is.
            yield return LoadShowcase();
            var far = Far();

            int coloured = 0, checkedMeshes = 0;
            foreach (Mesh mesh in RingMeshes(far))
            {
                if (mesh == null || mesh.vertexCount == 0) continue;
                checkedMeshes++;
                if (mesh.colors.Length == mesh.vertexCount) coloured++;
            }

            Assert.Greater(checkedMeshes, 0, "No far-terrain rings were built to inspect.");
            Assert.AreEqual(checkedMeshes, coloured,
                $"Only {coloured} of {checkedMeshes} far-terrain rings carry per-vertex colour. "
              + "A ring with positions and normals but no material signal can only ever render "
              + "as one flat tone, which is the grey range visible on the horizon.");
        }

        [UnityTest]
        public IEnumerator FarTerrainColourVariesAcrossTheRange()
        {
            // Colours being present is not enough — a constant colour array would pass the test
            // above. A mountain range spanning kilometres of altitude must not be monochrome.
            yield return LoadShowcase();
            var far = Far();

            var distinct = new HashSet<int>();
            foreach (Mesh mesh in RingMeshes(far))
            {
                if (mesh == null || mesh.vertexCount == 0) continue;
                Color[] colours = mesh.colors;
                if (colours.Length != mesh.vertexCount) continue;
                foreach (Color c in colours)
                    distinct.Add((Mathf.RoundToInt(c.r * 12f) << 8)
                               | (Mathf.RoundToInt(c.g * 12f) << 4)
                               |  Mathf.RoundToInt(c.b * 12f));
            }

            // Two, not more: ShowcaseWorld.SurfaceMaterialAt defines exactly two surface
            // materials, sand below BaseHeight and grass above, plus stone where the far mesh
            // rises over built content. Asserting more than the world actually has would force
            // the far field to invent colours the near field does not use, which is the drift
            // this file exists to prevent. What must never happen again is one.
            Assert.Greater(distinct.Count, 1,
                $"The whole far field uses {distinct.Count} distinguishable colour(s). Beaches "
              + "and vegetated ground are different materials in the voxel world and must not "
              + "collapse to a single tone on the horizon.");
        }

        [UnityTest]
        public IEnumerator FarFieldSurfaceMaterialAgreesWithTheVoxelWorld()
        {
            // The parity guard. Both renderers claim to describe one world. They already agree
            // on height; this asserts they agree on what the surface is made of, which is the
            // axis that has silently drifted.
            //
            // The voxel world's rule (ShowcaseWorld.MaterialAt) is: the surface voxel is sand
            // below BaseHeight and grass at or above it. Whatever the far mesh encodes, the
            // sand/grass split has to land in the same place or the shoreline moves as you
            // approach it.
            yield return LoadShowcase();
            var far = Far();
            var world = World(Showcase());

            var sandColours = new List<Color>();
            var grassColours = new List<Color>();

            foreach (Mesh mesh in RingMeshes(far))
            {
                if (mesh == null || mesh.vertexCount == 0) continue;
                Color[] colours = mesh.colors;
                if (colours.Length != mesh.vertexCount) continue;

                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i += 31)
                {
                    int voxelX = Mathf.RoundToInt(vertices[i].x / 0.1f);
                    int voxelZ = Mathf.RoundToInt(vertices[i].z / 0.1f);
                    int surface = TerrainSampler.HeightAt(voxelX, voxelZ, world.Seed);

                    // Skip columns the far mesh raised over built content; those are structure
                    // colours, not terrain, and the store does not claim material parity.
                    if (world.FarField != null
                        && world.FarField.HeightAt(voxelX, voxelZ) != int.MinValue) continue;

                    if (surface < ShowcaseWorld.BaseHeightVoxels) sandColours.Add(colours[i]);
                    else grassColours.Add(colours[i]);
                }
            }

            if (sandColours.Count == 0 || grassColours.Count == 0)
                Assert.Ignore("The sampled rings did not straddle the sand/grass altitude.");

            Color sand = Average(sandColours);
            Color grass = Average(grassColours);
            float separation = Mathf.Abs(sand.r - grass.r)
                             + Mathf.Abs(sand.g - grass.g)
                             + Mathf.Abs(sand.b - grass.b);

            Assert.Greater(separation, 0.10f,
                $"Far-field low ground averages {sand} and high ground {grass} — a separation of "
              + $"{separation:0.000}. The voxel world renders these as two different materials, "
              + "so the far field showing them as one colour is a visible discontinuity at the "
              + "streaming radius.");
        }

        private static Color Average(List<Color> colours)
        {
            float r = 0f, g = 0f, b = 0f;
            foreach (Color c in colours) { r += c.r; g += c.g; b += c.b; }
            return new Color(r / colours.Count, g / colours.Count, b / colours.Count);
        }

        // No pixel-level test of the far terrain appears here, deliberately.
        //
        // VoxelFarTerrain submits its rings with Graphics.DrawMesh, which registers a draw for
        // the normal render loop. A test that drives Camera.Render() by hand does not pick those
        // submissions up, so the captured frame contains the sky and the tree billboards and no
        // terrain whatsoever. An earlier version of this file asserted that ground pixels facing
        // the range were not grey; it passed against a mesh carrying no vertex colours at all,
        // because every pixel it scored was sky wash or foliage.
        //
        // That is worth knowing beyond this file: the far terrain is currently invisible to every
        // pixel test in the suite, the existing sky tests included, because the sky is a render
        // pass and the terrain is not. Restoring pixel coverage means changing how the rings are
        // drawn — a persistent MeshRenderer, or Graphics.RenderMesh — not a cleverer assertion.
        //
        // The colour defect is fully pinned down by the mesh-level tests above regardless: zero
        // of eight rings carry vertex colour, and the whole far field resolves to a single tone.

        private static Texture2D RenderCamera(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            shot.Apply();

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(target);
            return shot;
        }

        // ---------------------------------------------------------------------
        // 2. Coverage while streaming
        // ---------------------------------------------------------------------

        /// <summary>
        /// Whether anything at all is drawn over a world column: either the voxel region is
        /// resident, or the column lies outside ring 0's hole so the far mesh covers it.
        /// </summary>
        private static bool ColumnIsCovered(ShowcaseWorld world, VoxelFarTerrain far,
                                            Vector3 cameraPosition, Vector3 probe)
        {
            if (world.IsGenerated(ShowcaseWorld.RegionAt(probe))) return true;
            // Euclidean, matching both the voxel residency disc and ring 0's hole.
            float dx = probe.x - cameraPosition.x;
            float dz = probe.z - cameraPosition.z;
            return Mathf.Sqrt(dx * dx + dz * dz) >= far.HoleRadiusMetres;
        }

        [UnityTest]
        public IEnumerator FarFieldCoversGroundThatHasNotStreamedInYet()
        {
            // The reported symptom: sections appearing around the player on load. Ring 0's hole
            // is punched at its configured radius from the first frame, but the voxel regions
            // that are supposed to fill it take seconds to generate. Every column that is
            // neither resident nor covered by the far mesh is a hole the player watches fill.
            yield return LoadShowcase();
            var showcase = Showcase();
            var world = World(showcase);
            var far = Far();

            Vector3 camera = showcase.transform.position;
            var uncovered = new List<string>();

            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                for (float distance = 60f; distance < far.InnerRadiusMetres; distance += 60f)
                {
                    Vector3 probe = camera + direction * distance;
                    if (!ColumnIsCovered(world, far, camera, probe))
                        uncovered.Add($"{angle} deg at {distance:0} m");
                }
            }

            Assert.IsEmpty(uncovered,
                $"{uncovered.Count} columns inside the clipmap hole have neither resident voxels "
              + "nor far-field cover shortly after load, so they render as nothing until "
              + "streaming reaches them:\n  " + string.Join("\n  ", uncovered));
        }

        [UnityTest]
        public IEnumerator NothingPopsInAroundThePlayerWhileStreamingSettles()
        {
            // The same defect measured the way it is experienced: coverage should never improve
            // over time, because the far field should already be drawing everything the voxels
            // have not reached. If the uncovered count falls frame over frame, the player is
            // watching ground appear.
            yield return LoadShowcase();
            var showcase = Showcase();
            var world = World(showcase);
            var far = Far();

            int Uncovered()
            {
                Vector3 camera = showcase.transform.position;
                int count = 0;
                for (int angle = 0; angle < 360; angle += 30)
                for (float d = 60f; d < far.InnerRadiusMetres; d += 60f)
                {
                    Vector3 probe = camera + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * d;
                    if (!ColumnIsCovered(world, far, camera, probe)) count++;
                }
                return count;
            }

            int atLoad = Uncovered();
            for (int i = 0; i < 240; i++) yield return null;
            int settled = Uncovered();

            Assert.AreEqual(0, atLoad - settled,
                $"{atLoad - settled} columns went from uncovered to covered while streaming "
              + $"settled ({atLoad} uncovered at load, {settled} after). That difference is "
              + "exactly the terrain the player sees pop in around them; the far field should "
              + "have been drawing all of it at low detail from the first frame.");
        }

        [UnityTest]
        public IEnumerator ClipmapHoleNestsInsideTheVoxelResidencyDisc()
        {
            // A permanent version of the same mismatch, provable without rendering anything.
            //
            // Ring 0's hole is Chebyshev — a square of half-width InnerRadiusMetres. Voxel
            // residency is Euclidean — RefreshPending rejects on dx*dx + dz*dz > r*r. A square
            // does not nest inside a disc: the hole's corners sit at inner * sqrt(2), which is
            // 41% further out than its edges, and past that the voxels stop too.
            //
            // The result is four uncovered wedges on the diagonals that never close, however
            // long streaming runs.
            yield return LoadShowcase();
            var showcase = Showcase();
            var far = Far();

            for (int i = 0; i < 120; i++) yield return null;   // let streaming settle

            float voxelRadius = LoadRadiusRegions(showcase) * ShowcaseWorld.RegionMetres;

            Assert.LessOrEqual(far.HoleRadiusMetres, voxelRadius,
                $"Ring 0's hole is {far.HoleRadiusMetres:0} m but voxel residency is a disc of "
              + $"only {voxelRadius:0} m. Anywhere the hole reaches past the voxels, nothing is "
              + "drawn at all. (This originally failed because the hole was a Chebyshev square "
              + "against a Euclidean disc, so its diagonals overshot by 41%.)");
        }

        [UnityTest]
        public IEnumerator FarFieldInnerRadiusTracksActualResidencyNotAFixedNumber()
        {
            // The underlying design defect behind both coverage failures. The hole is sized once
            // from a configured radius and never consults what is resident, so it cannot be
            // correct during streaming, after eviction, or when the pool is under pressure.
            // Teleporting evicts everything nearby; the hole must open back up to compensate.
            yield return LoadShowcase();
            var showcase = Showcase();
            var world = World(showcase);
            var far = Far();

            for (int i = 0; i < 120; i++) yield return null;

            // Somewhere no region has ever been generated. Eviction empties the neighbourhood
            // and streaming starts again from nothing, which is the load case in the extreme.
            showcase.transform.position = new Vector3(6000f, 400f, 6000f);
            yield return null;
            yield return null;

            Vector3 camera = showcase.transform.position;
            int resident = 0, uncovered = 0, probes = 0;
            for (int angle = 0; angle < 360; angle += 45)
            for (float d = 60f; d < far.InnerRadiusMetres; d += 60f)
            {
                Vector3 probe = camera + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * d;
                probes++;
                if (world.IsGenerated(ShowcaseWorld.RegionAt(probe))) resident++;
                else if (!ColumnIsCovered(world, far, camera, probe)) uncovered++;
            }

            Assert.AreEqual(0, uncovered,
                $"Two frames after teleporting to unstreamed terrain, {uncovered} of {probes} "
              + $"probed columns are drawn by nothing ({resident} are resident). The hole is "
              + "sized from a radius fixed at startup, so it stays open across eviction and "
              + "re-streaming regardless of whether there are voxels to fill it. It has to be "
              + "derived from what the voxel world actually has.");
        }
    }
}
