using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Edits.Api
{
    /// <summary>
    /// The semantic cause of a voxel change. Replication transmits this compact event and every peer
    /// expands it with the same deterministic Edits.Runtime implementation rather than sending voxel effects.
    /// </summary>
    [Serializable]
    public struct AlterationEvent : IEquatable<AlterationEvent>
    {
        public byte kind;
        public uint tick;
        public int3 origin;

        /// <summary>
        /// Eight-byte shape union together with shapeData.
        /// Explosion: shapeData = radius in bricks.
        /// Brush: shapeKind packs extentX/extentY/extentZ/shapeType one byte each; shapeData = flags.
        /// RawBatch: reserved legacy fields until a canonical inline-batch representation exists.
        /// </summary>
        public uint shapeKind;
        public uint shapeData;

        public byte material;
        public uint seed;
        public ushort playerId;
        public ushort sequence;

        public const byte KindExplosion = 1;
        public const byte KindBrush = 2;
        public const byte KindRawBatch = 3;
        public const byte MaxMaterialIndex = 254;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AlterationEvent(byte kind, uint tick, int3 origin, ushort shapeRadius,
            byte material, uint seed, ushort playerId, ushort sequence)
        {
            this.kind = kind;
            this.tick = tick;
            this.origin = origin;
            this.shapeKind = (uint)kind;
            this.shapeData = shapeRadius;
            this.material = material;
            this.seed = seed;
            this.playerId = playerId;
            this.sequence = sequence;
        }

        /// <summary>Create a canonical axis-aligned cube brush using full dimensions in bricks.</summary>
        public static AlterationEvent CreateCubeBrush(
            uint tick,
            int3 origin,
            byte extentXBricks,
            byte extentYBricks,
            byte extentZBricks,
            byte material,
            uint seed,
            ushort playerId,
            ushort sequence,
            bool hardSurface = false)
        {
            return new AlterationEvent
            {
                kind = KindBrush,
                tick = tick,
                origin = origin,
                shapeKind = BrushShapeCodec.PackCube(extentXBricks, extentYBricks, extentZBricks),
                shapeData = hardSurface ? BrushShapeCodec.FlagHardSurface : 0u,
                material = material,
                seed = seed,
                playerId = playerId,
                sequence = sequence,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Validate()
        {
            if (kind < KindExplosion || kind > KindRawBatch) return false;
            if (tick == 0) return false;
            if (material > MaxMaterialIndex) return false;
            if (playerId == 0) return false;

            switch (kind)
            {
                case KindExplosion:
                    if (shapeData < 1 || shapeData >= (uint)VoxelReadGrid.BlocksPerRegionEdge)
                        return false;
                    break;

                case KindBrush:
                    if (!BrushShapeCodec.Validate(shapeKind, shapeData))
                        return false;
                    break;

                case KindRawBatch:
                    if (shapeKind == 0 && shapeData == 0)
                        return false;
                    break;
            }

            return true;
        }

        public bool ValidateWireFormat()
        {
            if (tick == 0) return false;
            if ((uint)kind > KindRawBatch) return false;
            if (material > MaxMaterialIndex) return false;
            if (playerId == 0) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Radius() => kind == KindExplosion ? (ushort)shapeData : (ushort)0;

        /// <summary>Full X/Y/Z dimensions in bricks for a canonical brush.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 BrushExtents() =>
            kind == KindBrush ? BrushShapeCodec.ExtentsBricks(shapeKind) : int3.zero;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte BrushShapeType() =>
            kind == KindBrush ? BrushShapeCodec.ShapeType(shapeKind) : (byte)0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BrushIsHardSurface() =>
            kind == KindBrush && BrushShapeCodec.IsHardSurface(shapeData);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort RawPrefabId() => kind == KindRawBatch ? (ushort)(shapeData & 0xFFFF) : (ushort)0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WireSize() => 32;

        public bool Equals(AlterationEvent other) =>
            kind == other.kind && tick == other.tick && math.all(origin == other.origin) &&
            shapeKind == other.shapeKind && shapeData == other.shapeData &&
            material == other.material && seed == other.seed &&
            playerId == other.playerId && sequence == other.sequence;

        public override bool Equals(object obj) => obj is AlterationEvent o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = kind.GetHashCode();
                h = (h * 397) ^ tick.GetHashCode();
                h = (h * 397) ^ origin.GetHashCode();
                h = (h * 397) ^ shapeKind.GetHashCode();
                h = (h * 397) ^ shapeData.GetHashCode();
                h = (h * 397) ^ material;
                h = (h * 397) ^ seed.GetHashCode();
                h = (h * 397) ^ playerId.GetHashCode();
                h = (h * 397) ^ sequence.GetHashCode();
                return h;
            }
        }

        public static bool operator ==(AlterationEvent a, AlterationEvent b) => a.Equals(b);
        public static bool operator !=(AlterationEvent a, AlterationEvent b) => !a.Equals(b);

        public override string ToString() =>
            kind switch
            {
                KindExplosion => $"Explosion(t={tick}, o={origin}, r={Radius()}, p={playerId})",
                KindBrush => $"Brush(t={tick}, o={origin}, e={BrushExtents()}, s={BrushShapeType()}, p={playerId})",
                KindRawBatch => $"RawBatch(t={tick}, o={origin}, p={playerId})",
                _ => $"Unknown(k={kind}, t={tick}, p={playerId})",
            };
    }
}
