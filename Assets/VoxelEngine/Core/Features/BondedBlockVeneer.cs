using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features.Emitters;
using VoxelEngine.Storage.Api;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Core.Features
{
    /// <summary>
    /// Deterministic coursed block veneer over a structural backing. Blocks are real authored
    /// primitives, while the backing carries load and closes the one-cell mortar joints. The
    /// definition is material-agnostic and works for ashlar, brick, timber blocks, tiles, or ice.
    /// </summary>
    public struct BondedBlockVeneerDefinition
    {
        public int3 Size;
        public int CoursePitch;
        public int NominalBlockWidth;
        public int JointWidth;
        public int Depth;
        public int CornerRadius;
        public uint Seed;
        public byte Material;
        public ushort SurfaceStyle;
        public byte Coating;

        public int EstimatedPrimitiveCount =>
            math.max(1, (Size.y + math.max(2, CoursePitch) - 1) / math.max(2, CoursePitch))
          * math.max(1, (Size.x + math.max(3, NominalBlockWidth) - 1)
                        / math.max(3, NominalBlockWidth) + 2);

        public bool IsValid => math.all(Size > 0) && CoursePitch >= 2
            && NominalBlockWidth >= 3 && JointWidth >= 1
            && JointWidth < math.min(CoursePitch, NominalBlockWidth)
            && Depth > 0 && Material != VoxelGrid.MaterialEmpty;

        public bool Emit(int3 origin, NativeList<Primitive> output)
        {
            if (!IsValid) return false;
            int order = output.Length;
            int blockHeight = CoursePitch - JointWidth;
            int blockDepth = math.min(Depth, Size.z);
            int course = 0;
            for (int y = 0; y < Size.y; y += CoursePitch, course++)
            {
                int height = math.min(blockHeight, Size.y - y);
                if (height <= 0) continue;

                int stagger = (course & 1) == 0 ? 0 : NominalBlockWidth / 2;
                int x = -stagger;
                int piece = 0;
                while (x < Size.x)
                {
                    int jitter = SignedJitter(Seed, course, piece);
                    int pitch = math.max(JointWidth + 2, NominalBlockWidth + jitter);
                    int start = math.max(0, x);
                    int end = math.min(Size.x, x + pitch - JointWidth);
                    int width = end - start;
                    if (width >= 2)
                    {
                        Primitive block = CurvedPrimitiveEmitter.RoundedBox(
                            origin + new int3(start, y, 0),
                            new int3(width, height, blockDepth), CornerRadius,
                            Material, SurfaceStyle, PrimitiveMode.Fill, order++, Coating,
                            extrusionAxis: 2);
                        block.SurfaceFlags = VoxelSurfaceFlags.PreserveFeature;
                        block.SurfaceDetail = PieceVariation(Seed, course, piece);
                        output.Add(block);
                    }
                    x += pitch;
                    piece++;
                }
            }
            return true;
        }

        private static int SignedJitter(uint seed, int course, int piece)
        {
            uint h = Hash(seed ^ (uint)(course + 1) * 0x9E3779B9u
                              ^ (uint)(piece + 1) * 0x85EBCA6Bu);
            return (int)(h % 3u) - 1;
        }

        private static byte PieceVariation(uint seed, int course, int piece) =>
            (byte)(1 + Hash(seed ^ (uint)(course + 3) * 0xC2B2AE35u
                                ^ (uint)(piece + 7) * 0x27D4EB2Fu) % 15u);

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            return value ^ (value >> 16);
        }
    }
}
