using System.Runtime.CompilerServices;

// Storage.Api owns the contract; the current Core assembly is the temporary implementation
// owner during Cutover 2. This friend is deleted when Storage.Runtime replaces Core.
[assembly: InternalsVisibleTo("VoxelEngine.Core")]
[assembly: InternalsVisibleTo("VoxelEngine.Storage.Runtime")]
