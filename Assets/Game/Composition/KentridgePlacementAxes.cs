using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Exact local-to-world horizontal axes used by Kentridge structure placement. Local authored
    /// structures enter along +Z with +X to their right; this mirrors the same quarter-turn vector
    /// transform used by the structure/ShapeProgram path.
    /// </summary>
    internal static class KentridgePlacementAxes
    {
        public static void Resolve(byte orientation, out Int2 inward, out Int2 right)
        {
            switch (orientation & 3)
            {
                case 1:
                    inward = new Int2(-1, 0);
                    right = new Int2(0, 1);
                    break;
                case 2:
                    inward = new Int2(0, -1);
                    right = new Int2(-1, 0);
                    break;
                case 3:
                    inward = new Int2(1, 0);
                    right = new Int2(0, -1);
                    break;
                default:
                    inward = new Int2(0, 1);
                    right = new Int2(1, 0);
                    break;
            }
        }
    }
}
