// Temporary compile-time import while CastleBuilder is moved into Structures.Runtime during
// Cutover 4. This does not expose a compatibility type or preserve the old Terrain API; delete
// this alias when CastleBuilder's source import is updated as part of its physical Runtime move.
global using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
