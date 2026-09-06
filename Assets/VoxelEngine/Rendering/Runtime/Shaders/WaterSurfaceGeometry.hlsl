#ifndef VOXEL_WATER_SURFACE_GEOMETRY_INCLUDED
#define VOXEL_WATER_SURFACE_GEOMETRY_INCLUDED

struct SurfaceVertex
{
    float3 position;
    float3 normal;
    uint material;
    uint active;
};

// Contiguous input remains only while the runtime water cache is being migrated.
StructuredBuffer<SurfaceVertex> _SurfaceVertices;
StructuredBuffer<uint> _SurfaceIndices;
uint _SurfaceIndexBase;
uint _SurfaceVertexBase;
uint _WaterPagedDraw;

struct WaterPagedDrawMetadata
{
    uint handle;
    uint indexCount;
    uint bank;
    uint padding;
};
StructuredBuffer<WaterPagedDrawMetadata> _PagedDrawMetadata;
StructuredBuffer<uint> _PagedDrawBucketState;
uint _PagedDrawBucket;
StructuredBuffer<SurfaceVertex> _PagedSurfaceVertices;
StructuredBuffer<uint> _PagedSurfaceIndices;
StructuredBuffer<uint> _PagedVertexPageTable;
StructuredBuffer<uint> _PagedIndexPageTable;
uint _PagedVertexPageSize;
uint _PagedIndexPageSize;
uint _PagedMaxVertexPagesPerChunk;
uint _PagedMaxIndexPagesPerChunk;

bool LoadWaterSurfaceVertex(uint vertexID, uint instanceID, out SurfaceVertex vertex)
{
    if (_WaterPagedDraw == 0u)
    {
        vertex = _SurfaceVertices[
            _SurfaceVertexBase + _SurfaceIndices[_SurfaceIndexBase + vertexID]];
        return true;
    }
    uint start = _PagedDrawBucketState[_PagedDrawBucket * 4u + 2u];
    WaterPagedDrawMetadata draw = _PagedDrawMetadata[start + instanceID];
    // Each bucket draws its maximum count; shorter instances must not fetch padding.
    if (vertexID >= draw.indexCount)
    {
        vertex = (SurfaceVertex)0;
        vertex.normal = float3(0.0, 1.0, 0.0);
        return false;
    }
    uint bank = draw.handle * 2u + draw.bank;
    uint indexTable = bank * _PagedMaxIndexPagesPerChunk + vertexID / _PagedIndexPageSize;
    uint physicalIndex = _PagedIndexPageTable[indexTable] * _PagedIndexPageSize
                       + vertexID % _PagedIndexPageSize;
    uint localVertex = _PagedSurfaceIndices[physicalIndex];
    uint vertexTable = bank * _PagedMaxVertexPagesPerChunk + localVertex / _PagedVertexPageSize;
    uint physicalVertex = _PagedVertexPageTable[vertexTable] * _PagedVertexPageSize
                        + localVertex % _PagedVertexPageSize;
    vertex = _PagedSurfaceVertices[physicalVertex];
    return true;
}
#endif
