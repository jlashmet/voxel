namespace MountingForce.WorldGen
{
    /// <summary>
    /// Small integer coordinate types owned by worldgen so the semantic layer does not depend on
    /// UnityEngine or Unity.Mathematics. Backend adapters convert these at their boundary.
    /// </summary>
    public readonly struct Int2
    {
        public readonly int X;
        public readonly int Y;

        public Int2(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public readonly struct Int3
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
