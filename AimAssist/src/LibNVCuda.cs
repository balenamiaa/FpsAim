using System.Runtime.InteropServices;
using Windows.Win32.Graphics.Direct3D11;

namespace AimAssist;

public unsafe static partial class LibNVCuda
{
    [LibraryImport("cudart64_12.dll", EntryPoint = "cudaGraphicsD3D11RegisterResource")]
    public static partial int CudaGraphicsD3D11RegisterResource(void** cuResource, ID3D11Resource* d3dResource, uint flags);

    [LibraryImport("cudart64_12.dll", EntryPoint = "cudaGraphicsMapResources")]
    public static partial int CudaGraphicsMapResources(int count, void** resource, nint flags);

    [LibraryImport("cudart64_12.dll", EntryPoint = "cudaGraphicsUnmapResources")]
    public static partial int CudaGraphicsUnmapResources(int count, void** resource, uint flags);

    [LibraryImport("cudart64_12.dll", EntryPoint = "cudaGraphicsResourceGetMappedPointer")]
    public static partial int CudaGraphicsResourceGetMappedPointer(void** pDevPtr, nuint* pSize, void* resource);

    // cudaGraphicsResourceSetMapFlags
    [LibraryImport("cudart64_12.dll", EntryPoint = "cudaGraphicsResourceSetMapFlags")]
    public static partial int CudaGraphicsResourceSetMapFlags(void* resource, uint flags);


    [Flags]
    public enum CUDA_RESOURCE_REGISTER_FLAG : uint
    {
        CUDA_RESOURCE_REGISTER_FLAG_NONE = 0,
        CUDA_RESOURCE_REGISTER_FLAG_READ_ONLY = 1,
        CUDA_RESOURCE_REGISTER_FLAG_WRITE_DISCARD = 2,
        CUDA_RESOURCE_REGISTER_FLAG_TEXTURE_GATHER = 4,
        CUDA_RESOURCE_REGISTER_FLAG_SURFACE_LOAD_STORE = 8
    }
    public const int CUDA_SUCCESS = 0;
}


