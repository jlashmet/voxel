using System;
using System.Runtime.CompilerServices;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// A region's pointer to one brick, packed into a single int.
    ///
    /// Three states, and the distinction is the entire memory argument of this engine:
    ///
    ///   Empty    -> no allocation at all
    ///   Uniform  -> no allocation; the material is encoded in the reference itself
    ///   Mixed    -> an index into <see cref="BrickPool"/>, 2112 bytes
    ///
    /// Only bricks containing a *surface* are mixed. Solid rock underground and open
    /// sky both cost nothing regardless of volume, so resident memory tracks surface
    /// area rather than world size.
    ///
    /// Encoding: values >= 0 are pool indices. Negative values encode a uniform
    /// material as -(material + 1), so -1 is empty (material 0), -2 is material 1,
    /// and so on. This keeps the common "is this brick allocated" test a single
    /// sign check.
    /// </summary>
    [Serializable]
    public readonly struct BrickRef : IEquatable<BrickRef>
    {
        public readonly int Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BrickRef(int value) => Value = value;

        /// <summary>An entirely empty brick. The default state of the world.</summary>
        public static BrickRef Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new BrickRef(-1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BrickRef FromPoolIndex(int poolIndex)
        {
#if UNITY_ASSERTIONS
            if (poolIndex < 0) throw new ArgumentOutOfRangeException(nameof(poolIndex));
#endif
            return new BrickRef(poolIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BrickRef Uniform(byte material) => new BrickRef(-(material + 1));

        /// <summary>True when this brick occupies a slot in the pool.</summary>
        public bool IsMixed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value >= 0;
        }

        /// <summary>True when this brick is entirely one material, including empty.</summary>
        public bool IsUniform
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value < 0;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value == -1;
        }

        /// <summary>Pool index. Only valid when <see cref="IsMixed"/>.</summary>
        public int PoolIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value;
        }

        /// <summary>Uniform material. Only valid when <see cref="IsUniform"/>.</summary>
        public byte UniformMaterial
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(-Value - 1);
        }

        public bool Equals(BrickRef other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BrickRef o && Equals(o);
        public override int GetHashCode() => Value;
        public static bool operator ==(BrickRef a, BrickRef b) => a.Value == b.Value;
        public static bool operator !=(BrickRef a, BrickRef b) => a.Value != b.Value;

        public override string ToString() =>
            IsMixed ? $"Mixed(#{Value})"
                    : IsEmpty ? "Empty"
                              : $"Uniform(mat {UniformMaterial})";
    }
}
