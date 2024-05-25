using Windows.Win32.Graphics.Direct3D11;

namespace AimAssist;

public class CudaGraphicsResource(nint handle) : IDisposable
{
    private readonly unsafe void* _handle = (void*)handle;

    public void Dispose()
    {
        Unmap();
        GC.SuppressFinalize(this);
    }

    public void Map()
    {
        unsafe
        {
            fixed (void** pHandle = &_handle)
            {
                var result = LibNVCuda.CudaGraphicsMapResources(1, pHandle, 0);
                if (result != LibNVCuda.CUDA_SUCCESS)
                {
                    throw new ScreenCaptureException($"Failed to map CUDA resources: {result}");
                }
            }
        }
    }

    public void Unmap()
    {
        unsafe
        {
            fixed (void** pHandle = &_handle)
            {
                var result = LibNVCuda.CudaGraphicsUnmapResources(1, pHandle, 0);
                if (result != LibNVCuda.CUDA_SUCCESS)
                {
                    throw new ScreenCaptureException($"Failed to unmap CUDA resources: {result}");
                }
            }
        }
    }

    public nint GetMappedPointer()
    {
        unsafe
        {
            var dataPtr = (void*)nint.Zero;
            nuint size = 0;

            var result = LibNVCuda.CudaGraphicsResourceGetMappedPointer(&dataPtr, &size, _handle);
            if (result != LibNVCuda.CUDA_SUCCESS)
            {
                throw new ScreenCaptureException($"Failed to get mapped pointer: {result}");
            }

            return (nint)dataPtr;
        }
    }

    public static unsafe CudaGraphicsResource RegisterCudaResource(ID3D11Buffer* buffer)
    {
        buffer->QueryInterface(typeof(ID3D11Resource).GUID, out var pResource);
        void* handle;
        var result = LibNVCuda.CudaGraphicsD3D11RegisterResource(&handle, (ID3D11Resource*)pResource,
            (uint)LibNVCuda.CUDA_RESOURCE_REGISTER_FLAG.CUDA_RESOURCE_REGISTER_FLAG_NONE);
        if (result != LibNVCuda.CUDA_SUCCESS)
        {
            throw new ScreenCaptureException($"Failed to register CUDA resource: {result}");
        }

        return new CudaGraphicsResource((nint)handle);
    }
}