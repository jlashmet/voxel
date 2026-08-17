using System;
using System.Collections.Generic;
using System.IO;
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

    public readonly struct ShowcaseWorldBakedRegion
    {
        public int3 Coord { get; }
        public uint SemanticHash { get; }
        public byte[] Payload { get; }

        public ShowcaseWorldBakedRegion(int3 coord, uint semanticHash, byte[] payload)
        {
            Coord = coord;
            SemanticHash = semanticHash;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }
    }

    /// <summary>
    /// Small, deliberately boring binary envelope around semantic region snapshots. The format is
    /// versioned and bounds-checked so a stale/corrupt bake fails before it mutates live storage.
    /// </summary>
    public static class ShowcaseWorldBakeCodec
    {
        public const string ResourcePath = "VoxelShowcase/ShowcaseWorld";
        public const int CurrentVersion = 1;

        private const uint Magic = 0x42535856; // "VXSB" little-endian.
        private const int MaxRegions = 4096;
        private const int MaxRegionPayloadBytes = 64 * 1024 * 1024;
        private const long MaxTotalPayloadBytes = 1024L * 1024L * 1024L;

        public static byte[] Serialize(ShowcaseWorldBake bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (bake.Regions.Count > MaxRegions)
                throw new InvalidDataException($"Showcase bake has {bake.Regions.Count} regions; maximum is {MaxRegions}.");

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

            long payloadBytes = 0;
            for (int i = 0; i < bake.Regions.Count; i++)
            {
                ShowcaseWorldBakedRegion region = bake.Regions[i];
                if (region.Payload.Length > MaxRegionPayloadBytes)
                    throw new InvalidDataException(
                        $"Region {region.Coord} snapshot is {region.Payload.Length} bytes; " +
                        $"maximum is {MaxRegionPayloadBytes}.");
                payloadBytes += region.Payload.Length;
                if (payloadBytes > MaxTotalPayloadBytes)
                    throw new InvalidDataException("Showcase bake exceeds the 1 GiB safety limit.");

                WriteInt3(writer, region.Coord);
                writer.Write(region.SemanticHash);
                writer.Write(region.Payload.Length);
                writer.Write(region.Payload);
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
            long payloadBytes = 0;
            for (int i = 0; i < regionCount; i++)
            {
                RequireRemaining(stream, sizeof(int) * 4 + sizeof(uint));
                int3 coord = ReadInt3(reader);
                uint hash = reader.ReadUInt32();
                int payloadLength = reader.ReadInt32();
                if (payloadLength < 0 || payloadLength > MaxRegionPayloadBytes)
                    throw new InvalidDataException(
                        $"Region {coord} has invalid snapshot length {payloadLength}.");

                payloadBytes += payloadLength;
                if (payloadBytes > MaxTotalPayloadBytes)
                    throw new InvalidDataException("Showcase bake exceeds the 1 GiB safety limit.");
                RequireRemaining(stream, payloadLength);
                byte[] payload = reader.ReadBytes(payloadLength);
                if (payload.Length != payloadLength)
                    throw new EndOfStreamException("Showcase bake ended inside a region snapshot.");
                regions.Add(new ShowcaseWorldBakedRegion(coord, hash, payload));
            }

            if (stream.Position != stream.Length)
                throw new InvalidDataException("Showcase bake contains trailing bytes. Re-bake the world.");

            return new ShowcaseWorldBake(
                seed, startupRadius, castleVoxels, featureVoxels, featureInstances,
                referenceArchMin, referenceArchMax, regions);
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
