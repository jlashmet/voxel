using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Presentation-only conversion from a render-independent procedural tree skeleton to Unity
    /// meshes. Tree identity, topology, collision and damage live behind VoxelEngine.Vegetation.Api.
    /// </summary>
    public static class ProceduralTreeMeshBuilder
    {
        // Dynamic tree damage used to allocate these large temporary lists for every generated LOD
        // and every detached limb. The renderer is main-thread only, so one prewarmed scratch set can
        // be safely reused sequentially and keeps destruction from creating multi-megabyte GC bursts.
        private sealed class MeshScratch
        {
            public readonly List<Vector3> Vertices = new(8192);
            public readonly List<Vector3> Normals = new(8192);
            public readonly List<Color> Colours = new(8192);
            public readonly List<Vector2> Uv0 = new(8192);
            public readonly List<Vector2> Uv1 = new(8192);
            public readonly List<int> BarkIndices = new(12288);
            public readonly List<int> LeafIndices = new(12288);

            public void Clear()
            {
                Vertices.Clear();
                Normals.Clear();
                Colours.Clear();
                Uv0.Clear();
                Uv1.Clear();
                BarkIndices.Clear();
                LeafIndices.Clear();
            }
        }

        private static readonly MeshScratch s_Scratch = new();

        public static Mesh Build(TreeRenderSnapshot tree, TreeRenderTier tier)
        {
            return Build(tree, tier, out _);
        }

        public static Mesh Build(TreeRenderSnapshot tree, TreeRenderTier tier, out Bounds bounds)
        {
            TreeRenderSkeleton skeleton = tree.Skeleton;
            MeshScratch scratch = s_Scratch;
            scratch.Clear();

            float radiusScale;
            int radialSegments;
            switch (tier)
            {
                case TreeRenderTier.High:
                    radiusScale = 1f;
                    radialSegments = 8;
                    break;
                case TreeRenderTier.Medium:
                    radiusScale = 0.95f;
                    radialSegments = 6;
                    break;
                default:
                    radiusScale = 0.9f;
                    radialSegments = 4;
                    break;
            }

            bounds = BuildBranchGeometry(in skeleton, radiusScale, radialSegments, scratch);
            BuildLeafGeometry(in skeleton, tier, scratch);

            var mesh = new Mesh
            {
                name = $"Tree-{tree.TreeId}-{tier}"
            };
            mesh.SetVertices(scratch.Vertices);
            mesh.SetNormals(scratch.Normals);
            mesh.SetColors(scratch.Colours);
            mesh.SetUVs(0, scratch.Uv0);
            mesh.SetUVs(1, scratch.Uv1);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(scratch.BarkIndices, 0, false);
            mesh.SetTriangles(scratch.LeafIndices, 1, false);
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Bounds BuildBranchGeometry(in TreeRenderSkeleton skeleton, float radiusScale,
                                                  int radialSegments, MeshScratch scratch)
        {
            bool hasPoint = false;
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);

            for (int i = 0; i < skeleton.Branches.Length; i++)
            {
                TreeRenderBranch branch = skeleton.Branches[i];
                float3 axis = branch.End - branch.Start;
                float length = math.length(axis);
                if (length <= 0.0001f) continue;
                axis /= length;

                float3 up = math.abs(axis.y) < 0.95f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                float3 tangent = math.normalize(math.cross(axis, up));
                float3 bitangent = math.cross(axis, tangent);
                float radius = math.max(0.01f, branch.Radius * radiusScale);

                int ringStart = scratch.Vertices.Count;
                for (int ring = 0; ring < 2; ring++)
                {
                    float3 centre = ring == 0 ? branch.Start : branch.End;
                    for (int segment = 0; segment < radialSegments; segment++)
                    {
                        float angle = (2f * math.PI * segment) / radialSegments;
                        float3 radial = math.cos(angle) * tangent + math.sin(angle) * bitangent;
                        float3 vertex = centre + radial * radius;
                        scratch.Vertices.Add(vertex);
                        scratch.Normals.Add(radial);
                        scratch.Colours.Add(branch.Colour);
                        scratch.Uv0.Add(new Vector2((float)segment / radialSegments, ring));
                        scratch.Uv1.Add(Vector2.zero);
                        min = math.min(min, vertex);
                        max = math.max(max, vertex);
                        hasPoint = true;
                    }
                }

                for (int segment = 0; segment < radialSegments; segment++)
                {
                    int next = (segment + 1) % radialSegments;
                    int a = ringStart + segment;
                    int b = ringStart + next;
                    int c = ringStart + radialSegments + segment;
                    int d = ringStart + radialSegments + next;
                    scratch.BarkIndices.Add(a);
                    scratch.BarkIndices.Add(c);
                    scratch.BarkIndices.Add(b);
                    scratch.BarkIndices.Add(b);
                    scratch.BarkIndices.Add(c);
                    scratch.BarkIndices.Add(d);
                }
            }

            if (!hasPoint)
                return new Bounds(Vector3.zero, Vector3.one * 0.1f);

            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds((min + max) * 0.5f, size);
        }

        private static void BuildLeafGeometry(in TreeRenderSkeleton skeleton, TreeRenderTier tier,
                                              MeshScratch scratch)
        {
            int step = tier switch
            {
                TreeRenderTier.High => 1,
                TreeRenderTier.Medium => 2,
                _ => 4,
            };

            for (int i = 0; i < skeleton.Leaves.Length; i += step)
            {
                TreeRenderLeaf leaf = skeleton.Leaves[i];
                float size = math.max(0.02f, leaf.Size);
                float3 normal = math.normalizesafe(leaf.Normal, new float3(0f, 1f, 0f));
                float3 tangent = math.normalizesafe(math.cross(normal, new float3(0f, 1f, 0f)),
                                                    new float3(1f, 0f, 0f));
                float3 bitangent = math.cross(normal, tangent);
                float3 centre = leaf.Position;

                AddLeafQuad(centre, tangent, bitangent, normal, size, leaf.Colour, scratch);
                if (tier == TreeRenderTier.High)
                    AddLeafQuad(centre, tangent, normal, -bitangent, size, leaf.Colour, scratch);
            }
        }

        private static void AddLeafQuad(float3 centre, float3 tangent, float3 bitangent, float3 normal,
                                        float size, Color colour, MeshScratch scratch)
        {
            int start = scratch.Vertices.Count;
            float3 halfT = tangent * (size * 0.5f);
            float3 halfB = bitangent * (size * 0.5f);
            scratch.Vertices.Add(centre - halfT - halfB);
            scratch.Vertices.Add(centre + halfT - halfB);
            scratch.Vertices.Add(centre + halfT + halfB);
            scratch.Vertices.Add(centre - halfT + halfB);
            for (int i = 0; i < 4; i++)
            {
                scratch.Normals.Add(normal);
                scratch.Colours.Add(colour);
                scratch.Uv1.Add(Vector2.zero);
            }
            scratch.Uv0.Add(new Vector2(0f, 0f));
            scratch.Uv0.Add(new Vector2(1f, 0f));
            scratch.Uv0.Add(new Vector2(1f, 1f));
            scratch.Uv0.Add(new Vector2(0f, 1f));
            scratch.LeafIndices.Add(start);
            scratch.LeafIndices.Add(start + 1);
            scratch.LeafIndices.Add(start + 2);
            scratch.LeafIndices.Add(start);
            scratch.LeafIndices.Add(start + 2);
            scratch.LeafIndices.Add(start + 3);
        }
    }
}
