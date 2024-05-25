using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Windows.Win32.Graphics.Direct3D11;

namespace AimAssist;

public readonly unsafe struct ScreenCaptureOutputAvailable(ID3D11DeviceContext4* context, ID3D11Buffer* stagingBuffer, ID3D11Buffer* dataBuffer, CudaGraphicsResource cudaResource, uint width, uint height, float[] preallocatedBuffer) : IScreenCaptureOutput
{
    private long[] Shape => [1, 3, height, width];
    private static readonly OrtMemoryInfo MemoryInfo = new(OrtMemoryInfo.allocatorCUDA, OrtAllocatorType.ArenaAllocator, 0, OrtMemType.CpuOutput);

    public Memory<float> GetCpuTensor()
    {
        D3D11_MAPPED_SUBRESOURCE mappedResource;
        context->CopyResource((ID3D11Resource*)stagingBuffer, (ID3D11Resource*)dataBuffer);
        context->Map((ID3D11Resource*)stagingBuffer, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mappedResource);
        fixed (float* p = preallocatedBuffer)
        {
            var numBytes = (uint)preallocatedBuffer.Length * sizeof(float);
            Buffer.MemoryCopy(mappedResource.pData, p, numBytes, numBytes);
        }
        context->Unmap((ID3D11Resource*)stagingBuffer, 0);

        return preallocatedBuffer;
    }

    public GpuMappedTensor GetGpuMappedTensor()
    {
        cudaResource.Map();
        var dataPtr = cudaResource.GetMappedPointer();
        var ortValue = OrtValue.CreateTensorValueWithData(MemoryInfo, TensorElementType.Float, Shape, dataPtr, preallocatedBuffer.Length * sizeof(float));
        return new GpuMappedTensor(ortValue, cudaResource);
    }
}
