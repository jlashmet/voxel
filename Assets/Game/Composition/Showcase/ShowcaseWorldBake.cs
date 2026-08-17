using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Portable startup image for the showcase world. Region payloads are Storage.Api semantic
    /// snapshots rather than BrickPool bytes, so the bake is independent of allocator layout and
    /// can be restored through the normal storage mutation boundary.
    /// </summary>
    public sealed class ShowcaseWorldBake
    {
        public uint Seed { get; }
        public int StartupRadiusRegions { get; }
        public long CastleVoxels { get; }
        public int FeatureVoxels { get; }
        public int FeatureInstances { get; }
        public int3 ReferenceArchMin { get; }
        public int3 ReferenceArchMax { get; }
        public IReadOnlyList<ShowcaseWorldBakedRegion> Regions { get; }

        public ShowcaseWorldBake(
            uint seed,
            int startupRadiusRegions,
            long castleVoxels,
            int featureVoxels,
            int featureInstances,
            int3 referenceArchMin,
            int3 referenceArchMax,
            IReadOnlyList<ShowcaseWorldBakedRegion> regions)
        {
            Seed = seed;
            StartupRadiusRegions = startupRadiusRegions;
            CastleVoxels = castleVoxels;
            FeatureVoxels = featureVoxels;
            FeatureInstances = featureInstances;
            ReferenceArchMin = referenceArchMin;
            ReferenceArchMax = referenceArchMax;
            Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        }
    }

    /// <summary>
    /// One region record in memory. A freshly captured bake carries the raw semantic snapshot;
    /// deserializing the on-disk format retains its compressed bytes so runtime never inflates the
    /// entire world image at once. <see cref="ShowcaseWorldBakeCodec.DecodeRegionPayload"/> expands
    /// one record immediately before Storage applies and verifies it.
    /// </summary>
    public readonly struct ShowcaseWorldBakedRegion
    {
        public int3 Coord { get; }
        public uint SemanticHash { get; }
        public byte[] Payload { get; }
        public int SemanticPayloadLength { get; }
        public bool IsCompressed { get; }

        public ShowcaseWorldBakedRegion(int3 coord, uint semanticHash, byte[] payload)
            : this(coord, semanticHash, payload, payload?.Length ?? 0, false)
        {
        }

        internal ShowcaseWorldBakedRegion(
            int3 coord,
            uint semanticHash,
            byte[] payload,
            int semanticPayloadLength,
            bool isCompressed)
        {
            Coord = coord;
            SemanticHash = semanticHash;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            SemanticPayloadLength = semanticPayloadLength;
            IsCompressed = isCompressed;
        }
    }

    /// <summary>
    /// Versioned binary envelope around semantic region snapshots. Semantic snapshots can be
    /// large for a heavily-authored 512^3 region because mixed bricks preserve four bytes per
    /// logical cell. The bake therefore Deflate-compresses every region independently. That keeps
    /// the startup asset compact and, more importantly, lets runtime inflate/apply one region at a
    /// time instead of materialising an uncompressed world-sized byte array.
    /// </summary>
    public static class ShowcaseWorldBakeCodec
    {
        public const string ResourcePath = "VoxelShowcase/ShowcaseWorld";
        public const int CurrentVersion = 2;

        // A fully mixed region is a little over 512 MiB in the semantic network representation.
        // This is a validation ceiling, not an expected asset size: the stored form is compressed.
        public const int MaxRawRegionPayloadBytes = 768 * 1024 * 1024;

        private const uint Magic = 0x42535856; // "VXSB" little-endian.
        private const byte CompressionDeflate = 1;
        private const int MaxRegions = 4096;
        private const int MaxStoredRegionPayloadBytes = 384 * 1024 * 1024;
        private const long MaxTotalRawPayloadBytes = 4L * 1024L * 1024L * 1024L;
        private const long MaxTotalStoredPayloadBytes = 1024L * 1024L * 1024L;

        public static byte[] Serialize(ShowcaseWorldBake bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (bake.Regions.Count > MaxRegions)
                throw new InvalidDataException(
                    $"Showcase bake has {bake.Regions.Count} regions; maximum is {MaxRegions}.");

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Magic);
            writer.Write(CurrentVersion);
            writer.Write(bake.Seed);
            writer.Write(bake.StartupRadiusRegions);
            writer.Write(bake.CastleVoxels);
            writer.Write(bake.FeatureVoxels);
            writer.Write(bake.FeatureInstances);
            WriteInt3(writer, bake.ReferenceArchMin);
            WriteInt3(writer, bake.ReferenceArchMax);
            writer.Write(bake.Regions.Count);

            long rawBytes = 0;
            long storedBytes = 0;
            for (int i = 0; i < bake.Regions.Count; i++)
            {
                ShowcaseWorldBakedRegion region = bake.Regions[i];
                int semanticLength = region.SemanticPayloadLength;
                if (semanticLength < 0 || semanticLength > MaxRawRegionPayloadBytes)
                    throw new InvalidDataException(
                        $"Region {region.Coord} semantic snapshot is {semanticLength} bytes; " +
                        $"maximum is {MaxRawRegionPayloadBytes}.");

                byte[] compressed = region.IsCompressed
                    ? region.Payload
                    : Compress(region.Payload);
                if (compressed.Length > MaxStoredRegionPayloadBytes)
                    throw new InvalidDataException(
                        $"Region {region.Coord} compressed snapshot is {compressed.Length} bytes; " +
                        $"maximum is {MaxStoredRegionPayloadBytes}.");

                rawBytes += semanticLength;
                storedBytes += compressed.Length;
                if (rawBytes > MaxTotalRawPayloadBytes)
                    throw new InvalidDataException(
                        "Showcase bake exceeds the 4 GiB uncompressed safety limit.");
                if (storedBytes > MaxTotalStoredPayloadBytes)
                    throw new InvalidDataException(
                        "Showcase bake exceeds the 1 GiB stored safety limit.");

                WriteInt3(writer, region.Coord);
                writer.Write(region.SemanticHash);
                writer.Write(semanticLength);
                writer.Write(CompressionDeflate);
                writer.Write(compressed.Length);
                writer.Write(compressed);
            }

            writer.Flush();
            return stream.ToArray();
        }

        public static ShowcaseWorldBake Deserialize(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream);

            RequireRemaining(stream, sizeof(uint) + sizeof(int));
            uint magic = reader.ReadUInt32();
            if (magic != Magic)
                throw new InvalidDataException("Not a Voxel Showcase baked world file.");

            int version = reader.ReadInt32();
            if (version != CurrentVersion)
                throw new InvalidDataException(
                    $"Showcase bake version {version} is not supported; expected {CurrentVersion}. Re-bake the world.");

            RequireRemaining(stream, sizeof(uint) + sizeof(int) + sizeof(long) + sizeof(int) * 8);
            uint seed = reader.ReadUInt32();
            int startupRadius = reader.ReadInt32();
            long castleVoxels = reader.ReadInt64();
            int featureVoxels = reader.ReadInt32();
            int featureInstances = reader.ReadInt32();
            int3 referenceArchMin = ReadInt3(reader);
            int3 referenceArchMax = ReadInt3(reader);
            int regionCount = reader.ReadInt32();
            if (regionCount < 0 || regionCount > MaxRegions)
                throw new InvalidDataException($"Invalid baked region count {regionCount}.");

            var regions = new List<ShowcaseWorldBakedRegion>(regionCount);
            long rawBytes = 0;
            long storedBytes = 0;
            for (int i = 0; i < regionCount; i++)
            {
                RequireRemaining(stream, sizeof(int) * 5 + sizeof(uint) + sizeof(byte));
                int3 coord = ReadInt3(reader);
                uint hash = reader.ReadUInt32();
                int semanticLength = reader.ReadInt32();
                byte compression = reader.ReadByte();
                int storedLength = reader.ReadInt32();

                if (semanticLength < 0 || semanticLength > MaxRawRegionPayloadBytes)
                    throw new InvalidDataException(
                        $"Region {coord} has invalid semantic snapshot length {semanticLength}.");
                if (compression != CompressionDeflate)
                    throw new InvalidDataException(
                        $"Region {coord} uses unsupported bake compression {compression}.");
                if (storedLength < 0 || storedLength > MaxStoredRegionPayloadBytes)
                    throw new InvalidDataException(
                        $"Region {coord} has invalid stored snapshot length {storedLength}.");

                rawBytes += semanticLength;
                storedBytes += storedLength;
                if (rawBytes > MaxTotalRawPayloadBytes)
                    throw new InvalidDataException(
                        "Showcase bake exceeds the 4 GiB uncompressed safety limit.");
                if (storedBytes > MaxTotalStoredPayloadBytes)
                    throw new InvalidDataException(
                        "Showcase bake exceeds the 1 GiB stored safety limit.");

                RequireRemaining(stream, storedLength);
                byte[] payload = reader.ReadBytes(storedLength);
                if (payload.Length != storedLength)
                    throw new EndOfStreamException("Showcase bake ended inside a region snapshot.");
                regions.Add(new ShowcaseWorldBakedRegion(
                    coord, hash, payload, semanticLength, isCompressed: true));
            }

            if (stream.Position != stream.Length)
                throw new InvalidDataException("Showcase bake contains trailing bytes. Re-bake the world.");

            return new ShowcaseWorldBake(
                seed, startupRadius, castleVoxels, featureVoxels, featureInstances,
                referenceArchMin, referenceArchMax, regions);
        }

        public static byte[] DecodeRegionPayload(ShowcaseWorldBakedRegion region)
        {
            if (!region.IsCompressed)
            {
                if (region.Payload.Length != region.SemanticPayloadLength)
                    throw new InvalidDataException(
                        $"Region {region.Coord} has inconsistent semantic payload metadata.");
                return region.Payload;
            }

            byte[] decoded = new byte[region.SemanticPayloadLength];
            using var input = new MemoryStream(region.Payload, writable: false);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: false);
            int offset = 0;
            while (offset < decoded.Length)
            {
                int read = deflate.Read(decoded, offset, decoded.Length - offset);
                if (read <= 0)
                    throw new InvalidDataException(
                        $"Region {region.Coord} compressed snapshot ended early.");
                offset += read;
            }

            if (deflate.ReadByte() != -1)
                throw new InvalidDataException(
                    $"Region {region.Coord} compressed snapshot expands beyond its declared length.");
            return decoded;
        }

        private static byte[] Compress(byte[] payload)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(payload, 0, payload.Length);
            return output.ToArray();
        }

        private static void WriteInt3(BinaryWriter writer, int3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static int3 ReadInt3(BinaryReader reader) =>
            new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

        private static void RequireRemaining(Stream stream, long bytes)
        {
            if (bytes < 0 || stream.Length - stream.Position < bytes)
                throw new EndOfStreamException("Showcase bake is truncated.");
        }
    }
}
