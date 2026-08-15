using System;
using System.Collections.Generic;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Mutable owner for retained surface primitives. Features add blocks during generation;
    /// read-only consumers observe them through <see cref="IProfileBlockReadSource"/>.
    /// </summary>
    public sealed class ProfileBlockStore : IProfileBlockReadSource, IProfileBlockWriter
    {
        private readonly List<ProfileBlock> _blocks = new();

        public uint Version { get; private set; }
        public int Count => _blocks.Count;
        public ProfileBlock this[int index] => _blocks[index];

        public void Add(in ProfileBlock block)
        {
            if (block.Axis > 2 || block.Material == VoxelGrid.MaterialEmpty
                || block.OuterRadiusQ4 <= block.InnerRadiusQ4
                || block.BackQ4 <= block.FrontQ4)
                throw new ArgumentException("Invalid profile block.", nameof(block));
            _blocks.Add(block);
            Version++;
        }

        public ProfileBlock[] Snapshot() => _blocks.ToArray();
    }
}
