using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Integer local-X/Z transform for archetypes authored with their primary entrance facing local
    /// South. Cardinal rotation preserves axis alignment and never introduces floating-point state.
    /// </summary>
    public static class StructureCardinalTransform
    {
        public static bool IsCardinal(Facing facing) =>
            facing == Facing.North || facing == Facing.East ||
            facing == Facing.South || facing == Facing.West;

        public static int2 Point(int2 local, Facing localSouthFaces)
        {
            switch (localSouthFaces)
            {
                case Facing.South: return local;
                case Facing.East: return new int2(-local.y, local.x);
                case Facing.North: return new int2(-local.x, -local.y);
                case Facing.West: return new int2(local.y, -local.x);
                default: throw new System.ArgumentOutOfRangeException(nameof(localSouthFaces));
            }
        }

        public static StructureFootprintRect Rect(in StructureFootprintRect local, Facing localSouthFaces)
        {
            int2 a = Point(local.Min, localSouthFaces);
            int2 b = Point(local.Min + new int2(local.Size.x, 0), localSouthFaces);
            int2 c = Point(local.Min + new int2(0, local.Size.y), localSouthFaces);
            int2 d = Point(local.Min + local.Size, localSouthFaces);
            int2 min = math.min(math.min(a, b), math.min(c, d));
            int2 max = math.max(math.max(a, b), math.max(c, d));
            return new StructureFootprintRect(min, max - min);
        }

        public static Facing FacingDirection(Facing local, Facing localSouthFaces)
        {
            if (!IsCardinal(local) || !IsCardinal(localSouthFaces))
                throw new System.ArgumentOutOfRangeException(nameof(local));

            int2 v = local == Facing.North ? new int2(0, 1)
                : local == Facing.East ? new int2(1, 0)
                : local == Facing.South ? new int2(0, -1)
                : new int2(-1, 0);
            int2 rotated = Point(v, localSouthFaces);
            if (rotated.y > 0) return Facing.North;
            if (rotated.x > 0) return Facing.East;
            if (rotated.y < 0) return Facing.South;
            return Facing.West;
        }

        public static RoofAxis Axis(RoofAxis local, Facing localSouthFaces)
        {
            if (!IsCardinal(localSouthFaces))
                throw new System.ArgumentOutOfRangeException(nameof(localSouthFaces));
            bool swapsAxes = localSouthFaces == Facing.East || localSouthFaces == Facing.West;
            if (!swapsAxes) return local;
            return local == RoofAxis.X ? RoofAxis.Z : RoofAxis.X;
        }
    }
}
