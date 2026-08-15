using System.Runtime.CompilerServices;

// Storage.Api owns the contract; Storage.Runtime is its physical implementation owner.
[assembly: InternalsVisibleTo("VoxelEngine.Storage.Runtime")]
