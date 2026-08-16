using Unity.Collections;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Read-only logical material behavior required by simulation clients. Physical palette
    /// storage, registration tables, and allocator representation remain in Storage.Runtime.
    /// </summary>
    public interface IMaterialSimulationCatalogue
    {
        uint Version { get; }
        byte GetHardness(byte materialId);
        DestructionClass GetDestructionClass(byte materialId);
        bool IsFlammable(byte materialId);
    }

    /// <summary>
    /// Small blittable snapshot of material behavior. Materials are fixed session vocabulary, so
    /// callers can capture once after registration and use the value in hot simulation loops
    /// without interface dispatch or a Runtime assembly dependency.
    /// </summary>
    public struct MaterialSimulationView
    {
        private struct Entry
        {
            public byte Hardness;
            public DestructionClass DestructionClass;
            public byte Flammable;
        }

        private FixedList128Bytes<Entry> _entries;
        public uint Version { get; private set; }

        public static MaterialSimulationView Capture<T>(in T source)
            where T : struct, IMaterialSimulationCatalogue
        {
            MaterialSimulationView view = default;
            view.Version = source.Version;
            for (int i = 0; i < 32; i++)
            {
                byte materialId = (byte)i;
                view._entries.Add(new Entry
                {
                    Hardness = source.GetHardness(materialId),
                    DestructionClass = source.GetDestructionClass(materialId),
                    Flammable = source.IsFlammable(materialId) ? (byte)1 : (byte)0,
                });
            }
            return view;
        }

        public byte GetHardness(byte materialId) =>
            materialId < _entries.Length ? _entries[materialId].Hardness : (byte)0;

        public DestructionClass GetDestructionClass(byte materialId) =>
            materialId < _entries.Length
                ? _entries[materialId].DestructionClass
                : DestructionClass.None;

        public bool IsFlammable(byte materialId) =>
            materialId < _entries.Length && _entries[materialId].Flammable != 0;

        public bool IsDestructible(byte materialId) =>
            GetDestructionClass(materialId) != DestructionClass.None;
    }
}
