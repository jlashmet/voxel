using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Presentation-only conversion from a render-independent procedural tree skeleton to Unity
    /// meshes. Tree identity, topology, collision and damage live in VoxelEngine.Core.
    /// </summary>
    public static class ProceduralTreeMeshBuilder
    {
        public static Mesh BuildMesh(ProceduralTreeSkeleton skeleton, int lod,
                                     HashSet<int> removedBranches = null) =>
            BuildMeshInternal(skeleton, lod, removedBranches, null);

        /// <summary>Builds only one detached connected branch subtree.</summary>
        public static Mesh BuildSubsetMesh(ProceduralTreeSkeleton skeleton, int lod,
                                           HashSet<int> includedBranches) =>
            BuildMeshInternal(skeleton, lod, null, includedBranches);

        /// <summary>
        /// Appends one tree directly into caller-owned mesh buffers. Batch rendering uses this path
        /// so healthy trees never need transient Unity Mesh objects merely to be combined again.
        /// </summary>
        public static void AppendMeshData(ProceduralTreeSkeleton skeleton, int lod,
                                          Vector3 positionOffset,
                                          List<Vector3> vertices,
                                          List<Vector3> normals,
                                          List<Color> colours,
                                          List<Vector2> uv0,
                                          List<Vector2> uv1,
                                          List<int> barkIndices,
                                          List<int> leafIndices,
                                          HashSet<int> removedBranches = null,
                                          HashSet<int> includedBranches = null)
        {
            lod = math.clamp(lod, 0, 2);
            int radialSides = lod == 0 ? 8 : lod == 1 ? 5 : 3;
            int leafStride = lod == 0 ? 1 : lod == 1 ? 2 : 4;
            float leafScale = lod == 0 ? 1f : lod == 1 ? 1.35f : 1.75f;
            int leafPlanes = lod < 2 ? 2 : 1;

            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                if (removedBranches != null && removedBranches.Contains(i)) continue;
                if (includedBranches != null && !includedBranches.Contains(i)) continue;

                TreeBranchSegment branch = skeleton.Branches[i];
                if (lod == 2 && branch.Level >= 3 && branch.RadiusStart < 0.035f) continue;
                AddTube(branch, skeleton.Profile, radialSides, positionOffset,
                        vertices, normals, colours, uv0, uv1, barkIndices);
            }

            for (int i = 0; i < skeleton.Leaves.Count; i += leafStride)
            {
                int parent = skeleton.LeafParents != null && i < skeleton.LeafParents.Length
                    ? skeleton.LeafParents[i] : -1;
                if (removedBranches != null && parent >= 0 && removedBranches.Contains(parent))
                    continue;
                if (includedBranches != null && (parent < 0 || !includedBranches.Contains(parent)))
                    continue;

                AddLeaf(skeleton.Leaves[i], leafScale, leafPlanes, positionOffset,
                        vertices, normals, colours, uv0, uv1, leafIndices);
            }
        }

        private static Mesh BuildMeshInternal(ProceduralTreeSkeleton skeleton, int lod,
                                              HashSet<int> removedBranches,
                                              HashSet<int> includedBranches)
        {
            var vertices = new List<Vector3>(8192);
            var normals = new List<Vector3>(8192);
            var colours = new List<Color>(8192);
            var uv0 = new List<Vector2>(8192);
            var uv1 = new List<Vector2>(8192);
            var barkIndices = new List<int>(12288);
            var leafIndices = new List<int>(12288);

            AppendMeshData(skeleton, lod, Vector3.zero,
                           vertices, normals, colours, uv0, uv1,
                           barkIndices, leafIndices, removedBranches, includedBranches);

            var mesh = new Mesh
            {
                name = $"ProceduralTree_LOD{math.clamp(lod, 0, 2)}",
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

        private static void AddTube(in TreeBranchSegment branch,
                                    in TreeSpeciesProfile profile, int sides,
                                    Vector3 positionOffset,
                                    List<Vector3> vertices, List<Vector3> normals,
                                    List<Color> colours, List<Vector2> uv0,
                                    List<Vector2> uv1, List<int> indices)
        {
            float3 tangent = math.normalizesafe(branch.End - branch.Start,
                                                new float3(0f, 1f, 0f));
            float3 reference = math.abs(tangent.y) < 0.90f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            float3 u = math.normalizesafe(math.cross(tangent, reference),
                                         new float3(1f, 0f, 0f));
            float3 v = math.normalizesafe(math.cross(tangent, u),
                                         new float3(0f, 0f, 1f));
            int baseVertex = vertices.Count;
            float colourT = math.saturate(branch.Level * 0.18f);
            float4 bark = math.lerp(profile.BarkColour, profile.BarkColourSecondary, colourT);
            var barkColour = new Color(bark.x, bark.y, bark.z, bark.w);

            for (int side = 0; side < sides; side++)
            {
                float angle = side * math.PI * 2f / sides;
                float3 radial = u * math.cos(angle) + v * math.sin(angle);
                vertices.Add((Vector3)(branch.Start + radial * branch.RadiusStart) + positionOffset);
                vertices.Add((Vector3)(branch.End + radial * branch.RadiusEnd) + positionOffset);
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

        private static void AddLeaf(in TreeLeafAnchor leaf, float scale, int planes,
                                    Vector3 positionOffset,
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
                Vector3 centre = (Vector3)leaf.Position + positionOffset;

                vertices.Add(centre - (Vector3)right * halfW - (Vector3)up * halfH);
                vertices.Add(centre + (Vector3)right * halfW - (Vector3)up * halfH);
                vertices.Add(centre + (Vector3)right * halfW + (Vector3)up * halfH);
                vertices.Add(centre - (Vector3)right * halfW + (Vector3)up * halfH);
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
    }
}
