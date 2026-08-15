using System.Runtime.CompilerServices;
using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// One authored parameter and the range it may take.
    ///
    /// The range is not documentation. Validation proves that every combination inside it produces
    /// geometry within the declared footprint, which is what lets the evaluator skip checking at
    /// runtime. A parameter without a range makes that proof impossible and pushes the check into
    /// the innermost loop of generation.
    /// </summary>
    public struct ParameterSpec
    {
        public FixedString32Bytes Name;

        /// <summary>Inclusive bounds, in whatever unit the parameter means — usually voxels.</summary>
        public int Min;
        public int Max;

        /// <summary>
        /// Draws snap to multiples of this. Keeps a wall thickness from landing on 3 when the
        /// shape program assumes even numbers, and makes variation read as deliberate rather than
        /// noisy.
        /// </summary>
        public int Quantum;

        public int Default;

        /// <summary>Clamps and snaps a value into the declared range.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Clamp(int value)
        {
            int q = Quantum > 1 ? Quantum : 1;

            if (value < Min) value = Min;
            if (value > Max) value = Max;

            // Snap toward Min so the result stays inside the range. Rounding to nearest could
            // land above Max, which would break the footprint proof that assumed it could not.
            int snapped = Min + ((value - Min) / q) * q;
            return snapped > Max ? Max : snapped;
        }
    }

    /// <summary>
    /// Resolved parameter values for one instance, indexed the same way as the definition's
    /// parameter list.
    ///
    /// Fixed capacity rather than an allocation: this is constructed per candidate during a scan
    /// that may consider hundreds of candidates per region, and a heap allocation there would be
    /// felt.
    /// </summary>
    public unsafe struct ParameterSet
    {
        public const int MaxParameters = 16;

        private fixed int _values[MaxParameters];

        public int Count;

        public int this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (uint)index < (uint)Count ? _values[index] : 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if ((uint)index >= (uint)MaxParameters) return;

                _values[index] = value;
                if (index >= Count) Count = index + 1;
            }
        }
    }
}
