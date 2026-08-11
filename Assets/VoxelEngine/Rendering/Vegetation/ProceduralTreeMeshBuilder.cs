using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using Random = Unity.Mathematics.Random;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Deterministic skeleton + mesh generation for procedural vegetation.
    ///
    /// Every LOD is produced from the same <see cref="TreeSkeleton"/>. Lower levels only reduce
    /// tube radial segments and foliage sampling; they never regenerate a different tree shape.
    /// The generated skeleton also carries a derived connectivity graph so destruction can cut a
    /// branch once and remove every dependent twig/leaf consistently across all visual LODs.
    /// </summary>
    public static class ProceduralTreeMeshBuilder
    {
        public struct BranchSegment
        {
            public float3 Start;
            public float3 End;
            public float RadiusStart;
            public float RadiusEnd;
            public int Level;
        }

        public struct LeafAnchor
        {
            public float3 Position;
            public float3 Direction;
            public float Size;
            public float Rotation;
            public float4 Colour;
            public TreeLeafStyle Style;
        }

        public sealed class TreeSkeleton
        {
            public readonly List<BranchSegment> Branches = new(256);
            public readonly List<LeafAnchor> Leaves = new(768);
            public TreeSpeciesProfile Profile;
            public float Height;

            // Derived after generation. Parents always precede children, which makes expanding a
            // sparse set of cuts into a connected removal mask a single forward pass.
            public int[] BranchParents;
            public int[] LeafParents;
        }

        private const int MaxBranchSegments = 640;
        private const int MaxLeaves = 1800;
        private const float Deg2Rad = math.PI / 180f;
        private static readonly float GoldenAngle = math.PI * (3f - math.sqrt(5f));

        public static TreeSkeleton GenerateSkeleton(in TreeInstance instance)
        {
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(instance.Species);
            float scale = math.max(0.05f, instance.Scale <= 0f ? 1f : instance.Scale);
            var rng = new Random(instance.Seed == 0 ? 1u : instance.Seed);
            var skeleton = new TreeSkeleton { Profile = profile };

            float height = rng.NextFloat(profile.HeightMin, profile.HeightMax) * scale;
            float trunkRadius = rng.NextFloat(profile.TrunkRadiusMin, profile.TrunkRadiusMax) * scale;
            skeleton.Height = height;

            int trunkSections = instance.Species == TreeSpecies.Pine ? 10 : 8;
            var trunkNodes = new float3[trunkSections + 1];
            trunkNodes[0] = float3.zero;
            float3 trunkDirection = new(0f, 1f, 0f);

            for (int i = 0; i < trunkSections; i++)
            {
                float t0 = i / (float)trunkSections;
                float t1 = (i + 1) / (float)trunkSections;
                float3 sideways = RandomPerpendicular(ref rng, trunkDirection);
                float bend = profile.Gnarliness * (instance.Species == TreeSpecies.Sakura ? 0.13f : 0.07f);
                trunkDirection = math.normalizesafe(
                    trunkDirection + sideways * bend + new float3(0f, 0.12f, 0f),
                    new float3(0f, 1f, 0f));

                float3 next = trunkNodes[i] + trunkDirection * (height / trunkSections);
                float r0 = trunkRadius * math.lerp(1f, profile.TrunkTaper, t0);
                float r1 = trunkRadius * math.lerp(1f, profile.TrunkTaper, t1);
                skeleton.Branches.Add(new BranchSegment
                {
                    Start = trunkNodes[i], End = next,
                    RadiusStart = r0, RadiusEnd = r1, Level = 0
                });
                trunkNodes[i + 1] = next;
            }

            int primaryCount = math.max(1, profile.PrimaryBranches);
            for (int i = 0; i < primaryCount && skeleton.Branches.Count < MaxBranchSegments; i++)
            {
                float distribution = primaryCount == 1 ? 1f : i / (float)(primaryCount - 1);
                float along = math.lerp(profile.BranchStart, 0.93f, distribution);
                along = math.saturate(along + rng.NextFloat(-0.035f, 0.035f));
                int nodeIndex = math.clamp((int)math.round(along * trunkSections), 1, trunkSections);
                float3 start = trunkNodes[nodeIndex];

                float azimuth = i * GoldenAngle + rng.NextFloat(-0.28f, 0.28f);
                float angle = rng.NextFloat(profile.BranchAngleMin, profile.BranchAngleMax) * Deg2Rad;
                float3 radial = new(math.cos(azimuth), 0f, math.sin(azimuth));
                float3 direction = math.normalizesafe(
                    radial * math.sin(angle) + new float3(0f, math.cos(angle), 0f)
                    + new float3(0f, profile.UpwardBias * 0.30f, 0f), radial);

                float heightFactor = instance.Species == TreeSpecies.Pine
                    ? math.lerp(1.20f, 0.48f, along)
                    : rng.NextFloat(0.78f, 1.18f);
                float length = height * profile.BranchLengthFactor * heightFactor;
                float radius = trunkRadius * math.lerp(0.64f, 0.34f, along);

                GrowBranch(skeleton, ref rng, in profile, instance.Species,
                           start, direction, length, radius, 1, scale);
            }

            // A few leaves near upper trunk tips prevent sparse species from getting a hollow cap.
            if (profile.LeafStyle != TreeLeafStyle.None && skeleton.Leaves.Count < MaxLeaves)
            {
                AddLeafCluster(skeleton, ref rng, in profile, trunkNodes[trunkSections],
                               trunkDirection, scale, math.max(3, profile.LeavesPerTip / 2));
            }

            ResolveTopology(skeleton);
            return skeleton;
        }

        private static void GrowBranch(TreeSkeleton skeleton, ref Random rng,
                                       in TreeSpeciesProfile profile, TreeSpecies species,
                                       float3 start, float3 direction, float length,
                                       float radius, int level, float scale)
        {
            if (skeleton.Branches.Count >= MaxBranchSegments || length < 0.12f || radius < 0.008f)
                return;

            int sections = level == 1 ? 4 : 3;
            float3 pos = start;
            direction = math.normalizesafe(direction, new float3(0f, 1f, 0f));
            var nodes = new float3[sections + 1];
            nodes[0] = start;

            for (int section = 0; section < sections && skeleton.Branches.Count < MaxBranchSegments; section++)
            {
                float progress0 = section / (float)sections;
                float progress1 = (section + 1) / (float)sections;
                float3 lateral = RandomPerpendicular(ref rng, direction);
                float wander = profile.Gnarliness * (0.14f + level * 0.055f);
                float droop = profile.Droop * progress1 * (0.10f + level * 0.045f);

                // Willow bends down strongly; pine resists lateral wandering; Sakura gets a
                // pronounced but still continuous asymmetric limb line.
                if (species == TreeSpecies.Willow) droop *= 2.2f;
                if (species == TreeSpecies.Pine) wander *= 0.45f;
                if (species == TreeSpecies.Sakura) wander *= 1.28f;

                direction = math.normalizesafe(
                    direction
                    + lateral * wander * rng.NextFloat(0.55f, 1.15f)
                    + new float3(0f, profile.UpwardBias * 0.08f - droop, 0f),
                    direction);

                float segmentLength = length / sections * rng.NextFloat(0.90f, 1.08f);
                float3 next = pos + direction * segmentLength;
                float r0 = radius * math.lerp(1f, 0.30f, progress0);
                float r1 = radius * math.lerp(1f, 0.30f, progress1);
                skeleton.Branches.Add(new BranchSegment
                {
                    Start = pos, End = next,
                    RadiusStart = r0, RadiusEnd = r1, Level = level
                });
                pos = next;
                nodes[section + 1] = next;
            }

            if (level >= profile.BranchLevels)
            {
                AddLeafCluster(skeleton, ref rng, in profile, pos, direction, scale,
                               profile.LeavesPerTip);
                return;
            }

            int childCount = math.max(1, profile.ChildBranches);
            for (int child = 0; child < childCount && skeleton.Branches.Count < MaxBranchSegments; child++)
            {
                int nodeIndex = math.clamp(sections - child % 2, 1, sections);
                float3 childStart = nodes[nodeIndex];
                float azimuth = child * GoldenAngle + rng.NextFloat(-0.42f, 0.42f);
                float3 tangent = direction;
                float3 u = RandomPerpendicular(ref rng, tangent);
                float3 v = math.normalizesafe(math.cross(tangent, u), new float3(0f, 0f, 1f));
                float3 radial = u * math.cos(azimuth) + v * math.sin(azimuth);
                float angle = rng.NextFloat(profile.BranchAngleMin, profile.BranchAngleMax) * Deg2Rad;
                float3 childDirection = math.normalizesafe(
                    tangent * math.cos(angle) + radial * math.sin(angle)
                    + new float3(0f, profile.UpwardBias * 0.18f, 0f), radial);

                float childLength = length * profile.BranchLengthDecay * rng.NextFloat(0.82f, 1.14f);
                float childRadius = radius * profile.BranchRadiusDecay;
                GrowBranch(skeleton, ref rng, in profile, species, childStart, childDirection,
                           childLength, childRadius, level + 1, scale);
            }

            // Populate intermediate branch tips too, especially for conifers and blossom crowns.
            if (profile.LeafStyle != TreeLeafStyle.None)
            {
                int intermediateCount = species == TreeSpecies.Sakura
                    ? math.max(3, profile.LeavesPerTip / 3)
                    : math.max(2, profile.LeavesPerTip / 5);
                AddLeafCluster(skeleton, ref rng, in profile, pos, direction, scale,
                               intermediateCount);
            }
        }

        private static void AddLeafCluster(TreeSkeleton skeleton, ref Random rng,
                                           in TreeSpeciesProfile profile,
                                           float3 centre, float3 direction, float scale, int count)
        {
            if (profile.LeafStyle == TreeLeafStyle.None || count <= 0) return;

            for (int i = 0; i < count && skeleton.Leaves.Count < MaxLeaves; i++)
            {
                float3 random = new float3(rng.NextFloat(-1f, 1f),
                                           rng.NextFloat(-0.55f, 0.85f),
                                           rng.NextFloat(-1f, 1f));
                random = math.normalizesafe(random, new float3(0f, 1f, 0f));
                float spread = profile.LeafSpread * scale * rng.NextFloat(0.15f, 1f);
                float size = profile.LeafSize * scale
                           * rng.NextFloat(1f - profile.LeafSizeVariance,
                                           1f + profile.LeafSizeVariance);
                float colourT = rng.NextFloat();
                float4 colour = math.lerp(profile.LeafColourA, profile.LeafColourB, colourT);

                // Blossom clusters are dense bunches around twig tips rather than broad cards
                // distributed through the whole crown.
                if (profile.LeafStyle == TreeLeafStyle.Blossom)
                {
                    spread *= 0.58f;
                    size *= rng.NextFloat(0.82f, 1.14f);
                }

                skeleton.Leaves.Add(new LeafAnchor
                {
                    Position = centre + random * spread,
                    Direction = math.normalizesafe(direction + random * 0.45f,
                                                   new float3(0f, 1f, 0f)),
                    Size = size,
                    Rotation = rng.NextFloat(0f, math.PI * 2f),
                    Colour = colour,
                    Style = profile.LeafStyle,
                });
            }
        }

        /// <summary>
        /// Derives parent links from exact shared node positions. The grammar emits each parent
        /// before its descendants, and branch starts are copied from existing node positions, so
        /// this is deterministic and avoids storing render topology in the semantic TreeInstance.
        /// </summary>
        private static void ResolveTopology(TreeSkeleton skeleton)
        {
            int branchCount = skeleton.Branches.Count;
            skeleton.BranchParents = new int[branchCount];
            const float nodeEpsilonSq = 1e-8f;

            for (int i = 0; i < branchCount; i++)
            {
                BranchSegment branch = skeleton.Branches[i];
                int parent = -1;
                int bestLevelDelta = int.MaxValue;

                for (int j = i - 1; j >= 0; j--)
                {
                    BranchSegment candidate = skeleton.Branches[j];
                    int levelDelta = branch.Level - candidate.Level;
                    if (levelDelta < 0 || levelDelta > 1) continue;
                    if (math.lengthsq(candidate.End - branch.Start) > nodeEpsilonSq) continue;

                    // Same-level continuation is the strongest match. Otherwise this is the
                    // first segment of a child branch attached to its parent's node.
                    if (levelDelta < bestLevelDelta)
                    {
                        parent = j;
                        bestLevelDelta = levelDelta;
                        if (levelDelta == 0) break;
                    }
                }
                skeleton.BranchParents[i] = parent;
            }

            skeleton.LeafParents = new int[skeleton.Leaves.Count];
            for (int i = 0; i < skeleton.Leaves.Count; i++)
            {
                float3 p = skeleton.Leaves[i].Position;
                int nearest = -1;
                float nearestSq = float.PositiveInfinity;
                for (int j = 0; j < branchCount; j++)
                {
                    BranchSegment branch = skeleton.Branches[j];
                    float distanceSq = DistanceToSegmentSq(p, branch.Start, branch.End);
                    if (distanceSq >= nearestSq) continue;
                    nearestSq = distanceSq;
                    nearest = j;
                }
                skeleton.LeafParents[i] = nearest;
            }
        }

        /// <summary>
        /// Expands directly cut branches through the skeleton connectivity. The caller can store
        /// only actual cuts; all downstream branch/twig removal is reconstructed deterministically.
        /// </summary>
        public static void ResolveRemovedBranches(TreeSkeleton skeleton,
                                                  IReadOnlyCollection<int> directCuts,
                                                  HashSet<int> resolved)
        {
            resolved.Clear();
            if (directCuts == null || directCuts.Count == 0) return;

            foreach (int cut in directCuts)
                if ((uint)cut < (uint)skeleton.Branches.Count)
                    resolved.Add(cut);

            int[] parents = skeleton.BranchParents;
            if (parents == null || parents.Length != skeleton.Branches.Count) return;
            for (int i = 0; i < parents.Length; i++)
            {
                int parent = parents[i];
                if (parent >= 0 && resolved.Contains(parent)) resolved.Add(i);
            }
        }

        public static Mesh BuildMesh(TreeSkeleton skeleton, int lod,
                                     HashSet<int> removedBranches = null)
        {
            lod = math.clamp(lod, 0, 2);
            int radialSides = lod == 0 ? 8 : lod == 1 ? 5 : 3;
            int leafStride = lod == 0 ? 1 : lod == 1 ? 2 : 4;
            float leafScale = lod == 0 ? 1f : lod == 1 ? 1.35f : 1.75f;
            int leafPlanes = lod < 2 ? 2 : 1;

            var vertices = new List<Vector3>(8192);
            var normals = new List<Vector3>(8192);
            var colours = new List<Color>(8192);
            var uv0 = new List<Vector2>(8192);
            var uv1 = new List<Vector2>(8192);
            var barkIndices = new List<int>(12288);
            var leafIndices = new List<int>(12288);

            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                if (removedBranches != null && removedBranches.Contains(i)) continue;
                BranchSegment branch = skeleton.Branches[i];
                // The far mesh keeps the shared skeleton but drops the thinnest tertiary twigs.
                // Their foliage remains and preserves the crown silhouette.
                if (lod == 2 && branch.Level >= 3 && branch.RadiusStart < 0.035f) continue;
                AddTube(branch, skeleton.Profile, radialSides,
                        vertices, normals, colours, uv0, uv1, barkIndices);
            }

            for (int i = 0; i < skeleton.Leaves.Count; i += leafStride)
            {
                int parent = skeleton.LeafParents != null && i < skeleton.LeafParents.Length
                    ? skeleton.LeafParents[i] : -1;
                if (removedBranches != null && parent >= 0 && removedBranches.Contains(parent))
                    continue;
                AddLeaf(skeleton.Leaves[i], leafScale, leafPlanes,
                        vertices, normals, colours, uv0, uv1, leafIndices);
            }

            var mesh = new Mesh
            {
                name = $"ProceduralTree_LOD{lod}",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(barkIndices, 0, false);
            mesh.SetTriangles(leafIndices, 1, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTube(in BranchSegment branch, in TreeSpeciesProfile profile,
                                    int sides,
                                    List<Vector3> vertices, List<Vector3> normals,
                                    List<Color> colours, List<Vector2> uv0,
                                    List<Vector2> uv1, List<int> indices)
        {
            float3 tangent = math.normalizesafe(branch.End - branch.Start,
                                                new float3(0f, 1f, 0f));
            float3 reference = math.abs(tangent.y) < 0.90f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            float3 u = math.normalizesafe(math.cross(tangent, reference), new float3(1f, 0f, 0f));
            float3 v = math.normalizesafe(math.cross(tangent, u), new float3(0f, 0f, 1f));
            int baseVertex = vertices.Count;
            float colourT = math.saturate(branch.Level * 0.18f);
            float4 bark = math.lerp(profile.BarkColour, profile.BarkColourSecondary, colourT);
            var barkColour = new Color(bark.x, bark.y, bark.z, bark.w);

            for (int side = 0; side < sides; side++)
            {
                float angle = side * math.PI * 2f / sides;
                float3 radial = u * math.cos(angle) + v * math.sin(angle);
                vertices.Add((Vector3)(branch.Start + radial * branch.RadiusStart));
                vertices.Add((Vector3)(branch.End + radial * branch.RadiusEnd));
                normals.Add((Vector3)radial);
                normals.Add((Vector3)radial);
                colours.Add(barkColour);
                colours.Add(barkColour);
                float x = side / (float)sides;
                uv0.Add(new Vector2(x, 0f));
                uv0.Add(new Vector2(x, 1f));
                uv1.Add(new Vector2(branch.Level, 0f));
                uv1.Add(new Vector2(branch.Level, 0f));
            }

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = baseVertex + side * 2;
                int b = baseVertex + next * 2;
                int c = a + 1;
                int d = b + 1;
                indices.Add(a); indices.Add(c); indices.Add(b);
                indices.Add(b); indices.Add(c); indices.Add(d);
            }
        }

        private static void AddLeaf(in LeafAnchor leaf, float scale, int planes,
                                    List<Vector3> vertices, List<Vector3> normals,
                                    List<Color> colours, List<Vector2> uv0,
                                    List<Vector2> uv1, List<int> indices)
        {
            float3 up = math.normalizesafe(
                math.lerp(new float3(0f, 1f, 0f), leaf.Direction, 0.22f),
                new float3(0f, 1f, 0f));
            float size = leaf.Size * scale;
            var colour = new Color(leaf.Colour.x, leaf.Colour.y, leaf.Colour.z, leaf.Colour.w);

            for (int plane = 0; plane < planes; plane++)
            {
                float angle = leaf.Rotation + plane * math.PI * 0.5f;
                float3 horizontal = new(math.cos(angle), 0f, math.sin(angle));
                float3 right = math.normalizesafe(horizontal, new float3(1f, 0f, 0f));
                float3 normal = math.normalizesafe(math.cross(right, up), new float3(0f, 0f, 1f));
                int start = vertices.Count;
                float halfW = size * (leaf.Style == TreeLeafStyle.Needle ? 0.28f : 0.50f);
                float halfH = size * (leaf.Style == TreeLeafStyle.Narrow ? 0.72f : 0.50f);

                vertices.Add((Vector3)(leaf.Position - right * halfW - up * halfH));
                vertices.Add((Vector3)(leaf.Position + right * halfW - up * halfH));
                vertices.Add((Vector3)(leaf.Position + right * halfW + up * halfH));
                vertices.Add((Vector3)(leaf.Position - right * halfW + up * halfH));
                for (int i = 0; i < 4; i++)
                {
                    normals.Add((Vector3)normal);
                    colours.Add(colour);
                    uv1.Add(new Vector2((float)leaf.Style, 0f));
                }
                uv0.Add(new Vector2(0f, 0f));
                uv0.Add(new Vector2(1f, 0f));
                uv0.Add(new Vector2(1f, 1f));
                uv0.Add(new Vector2(0f, 1f));
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }
        }

        private static float DistanceToSegmentSq(float3 point, float3 a, float3 b)
        {
            float3 ab = b - a;
            float denom = math.lengthsq(ab);
            if (denom <= 1e-10f) return math.lengthsq(point - a);
            float t = math.saturate(math.dot(point - a, ab) / denom);
            return math.lengthsq(point - (a + ab * t));
        }

        private static float3 RandomPerpendicular(ref Random rng, float3 direction)
        {
            direction = math.normalizesafe(direction, new float3(0f, 1f, 0f));
            float3 reference = math.abs(direction.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            float3 u = math.normalizesafe(math.cross(direction, reference), new float3(1f, 0f, 0f));
            float3 v = math.normalizesafe(math.cross(direction, u), new float3(0f, 0f, 1f));
            float angle = rng.NextFloat(0f, math.PI * 2f);
            return math.normalizesafe(u * math.cos(angle) + v * math.sin(angle), u);
        }
    }
}
