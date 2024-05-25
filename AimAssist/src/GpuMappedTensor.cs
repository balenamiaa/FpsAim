using Microsoft.ML.OnnxRuntime;

namespace AimAssist;

public class GpuMappedTensor(OrtValue tensor, CudaGraphicsResource cudaResource) : IDisposable
{
    private readonly CudaGraphicsResource _cudaResource = cudaResource;

    public OrtValue Tensor { get; private set; } = tensor;

    public void Dispose()
    {
        _cudaResource.Unmap();
    }
}


