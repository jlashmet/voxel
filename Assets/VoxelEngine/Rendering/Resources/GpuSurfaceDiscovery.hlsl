#ifndef VOXEL_SURFACE_DISCOVERY_INCLUDED
#define VOXEL_SURFACE_DISCOVERY_INCLUDED
StructuredBuffer<uint> _DiscoveryWords;
RWStructuredBuffer<uint> _DiscoveryMap;
uint _DiscoveryRegionCount;
uint _DiscoveryMapCapacity;
uint RegionHash(int3 region)
{
    uint3 value=asuint(region);
    return (value.x*73856093u ^ value.y*19349663u ^ value.z*83492791u)&(_DiscoveryMapCapacity-1u);
}

uint DiscoveryCellState(int3 fine)
{
    int3 region=fine>>3;
    uint slot=RegionHash(region);
    for(uint probe=0u;probe<_DiscoveryMapCapacity;probe++)
    {
        uint entry=_DiscoveryMap[slot];
        if(entry==0u)return 0u;
        uint base=(entry-1u)*20u;
        int3 key=int3(_DiscoveryWords[base],_DiscoveryWords[base+1u],_DiscoveryWords[base+2u]);
        if(all(key==region))
        {
            if(_DiscoveryWords[base+3u]==0u)return 0u;
            uint3 local=asuint(fine)&7u;
            uint bit=local.x+8u*(local.y+8u*local.z);
            return (_DiscoveryWords[base+4u+(bit>>5)]&(1u<<(bit&31u)))==0u?1u:2u;
        }
        slot=(slot+1u)&(_DiscoveryMapCapacity-1u);
    }
    return 0u;
}


#endif
