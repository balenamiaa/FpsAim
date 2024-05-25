using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.System.Com;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.UI.HiDpi;
using D3D11_BIND_FLAG = Windows.Win32.Graphics.Direct3D11.D3D11_BIND_FLAG;
using D3D11_BUFFER_DESC = Windows.Win32.Graphics.Direct3D11.D3D11_BUFFER_DESC;
using D3D11_CPU_ACCESS_FLAG = Windows.Win32.Graphics.Direct3D11.D3D11_CPU_ACCESS_FLAG;
using D3D11_SUBRESOURCE_DATA = Windows.Win32.Graphics.Direct3D11.D3D11_SUBRESOURCE_DATA;
using D3D11_TEXTURE2D_DESC = Windows.Win32.Graphics.Direct3D11.D3D11_TEXTURE2D_DESC;
using D3D11_USAGE = Windows.Win32.Graphics.Direct3D11.D3D11_USAGE;
using DXGI_FORMAT = Windows.Win32.Graphics.Dxgi.Common.DXGI_FORMAT;
using DXGI_GPU_PREFERENCE = Windows.Win32.Graphics.Dxgi.DXGI_GPU_PREFERENCE;

namespace AimAssist;

[SupportedOSPlatform("windows10.0.17763")]
public sealed unsafe class ScreenCapturer : IDisposable
{
    private readonly ILogger _logger;

    private readonly IDXGIAdapter4* _adapter;
    private readonly IDXGIOutput6* _output;
    private IDXGIOutputDuplication* _duplication;

    private readonly ID3D11Device4* _device;
    private readonly ID3D11DeviceContext4* _context;

    private readonly ID3D11Buffer* _captureRegionBuffer;
    private readonly ID3D11Texture2D* _capturedFrameTexture;
    private readonly ID3D11Buffer* _outputBuffer;
    private readonly ID3D11ComputeShader* _computeShader;
    private readonly ID3D11UnorderedAccessView* _uav;
    private readonly ID3D11Buffer* _stagingBuffer;

    private readonly CudaGraphicsResource _outputBufferCudaResource;

    public readonly uint ScreenWidth;
    public readonly uint ScreenHeight;
    public readonly uint CaptureWidth;
    public readonly uint CaptureHeight;
    private readonly D3D11_BOX _centerBox;
    private readonly float[] _preallocatedBuffer;

    public long LastFrameTimeTicks { get; private set; }

    public ScreenCapturer(uint gpuIndex, uint monitorIndex, uint captureWidth, uint captureHeight,
        ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        CaptureWidth = captureWidth;
        CaptureHeight = captureHeight;
        _preallocatedBuffer = new float[3 * captureWidth * captureHeight];

        Initialize(gpuIndex, monitorIndex, out _adapter, out _output, out ScreenWidth, out ScreenHeight);

        _centerBox = new D3D11_BOX
        {
            left = (ScreenWidth - captureWidth) / 2,
            top = (ScreenHeight - captureHeight) / 2,
            front = 0,
            right = (ScreenWidth + captureWidth) / 2,
            bottom = (ScreenHeight + captureHeight) / 2,
            back = 1
        };

        fixed (ID3D11Device4** device = &_device)
        fixed (ID3D11DeviceContext4** context = &_context)
        {
            CreateDeviceResources(device, context);
        }

        IDXGIOutputDuplication* duplication;
        _output->DuplicateOutput1((IUnknown*)_device, 0,
            [DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT], &duplication);

        _duplication = duplication;
        _captureRegionBuffer = CreateCaptureRegionBuffer(_device, CaptureWidth, CaptureHeight);
        _outputBuffer = CreateOutputBuffer(_device, CaptureWidth, CaptureHeight);
        _outputBufferCudaResource = CudaGraphicsResource.RegisterCudaResource(_outputBuffer);
        _capturedFrameTexture = CreateCapturedFrameTexture(_device, CaptureWidth, CaptureHeight);
        _computeShader = CreateComputeShader(_device);
        _uav = CreateUav(_device, _outputBuffer, CaptureWidth, CaptureHeight);
        _stagingBuffer = CreateStagingBuffer(_device, CaptureWidth, CaptureHeight);


        fixed (ID3D11Buffer** captureRegionBuffer = &_captureRegionBuffer)
        {
            _context->CSSetConstantBuffers(0, 1, captureRegionBuffer);
        }

        fixed (ID3D11UnorderedAccessView** uav = &_uav)
        {
            _context->CSSetUnorderedAccessViews(0, uav, [1]);
        }

        _context->CSSetShader(_computeShader, null, 0);
    }

    public void Dispose()
    {
        _duplication->Release();
        _device->Release();
        _context->Release();
        _captureRegionBuffer->Release();
        _capturedFrameTexture->Release();
        _outputBuffer->Release();
        _computeShader->Release();
        _uav->Release();
        _stagingBuffer->Release();
        _outputBufferCudaResource.Dispose();
    }

    public IScreenCaptureOutput CaptureFrame()
    {
        try
        {
            var hr = AcquireNextFrame(0, _duplication, out _, out var desktopResource);

            if (hr != HRESULT.S_OK)
            {
                if (hr == HRESULT.DXGI_ERROR_WAIT_TIMEOUT)
                {
                    return new ScreenCaptureOutputNotAvailable();
                }

                if (hr == HRESULT.DXGI_ERROR_ACCESS_LOST)
                {
                    ReacquireDuplication();
                    return new ScreenCaptureOutputNotAvailable();
                }

                if (hr == HRESULT.DXGI_ERROR_ACCESS_DENIED)
                {
                    return new ScreenCaptureOutputNotAvailable();
                }

                throw new COMException("Failed to acquire next frame", hr);
            }

            var desktopResourceD3D11 = QueryInterface<ID3D11Resource>((IUnknown*)desktopResource);
            var captureFrameResource = QueryInterface<ID3D11Resource>((IUnknown*)_capturedFrameTexture);

            _context->CopySubresourceRegion(captureFrameResource, 0, 0, 0, 0, desktopResourceD3D11, 0, _centerBox);

            ID3D11ShaderResourceView* shaderResource;
            _device->CreateShaderResourceView(captureFrameResource, (D3D11_SHADER_RESOURCE_VIEW_DESC?)null,
                &shaderResource);
            _context->CSSetShaderResources(0, 1, &shaderResource);

            uint dispatchX = (uint)Math.Ceiling(CaptureWidth / 8.0);
            uint dispatchY = (uint)Math.Ceiling(CaptureHeight / 8.0);
            _context->Dispatch(dispatchX, dispatchY, 1);

            _duplication->ReleaseFrame();

            LastFrameTimeTicks = Stopwatch.GetTimestamp();

            return new ScreenCaptureOutputAvailable(_context, _stagingBuffer, _outputBuffer, _outputBufferCudaResource,
                CaptureWidth, CaptureHeight, _preallocatedBuffer);
        }
        catch (UnauthorizedAccessException e)
        {
            _logger.LogError(e, "Failed to capture frame");
            ReacquireDuplication();
            return new ScreenCaptureOutputNotAvailable();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to capture frame");
            return new ScreenCaptureOutputNotAvailable();
        }
    }

    private void ReacquireDuplication()
    {
        _duplication->Release();
        _duplication = null;
        while ((nint)_duplication == nint.Zero)
        {
            Thread.Sleep(1000);
            try
            {
                fixed (IDXGIOutputDuplication** duplication = &_duplication)
                {
                    _output->DuplicateOutput1((IUnknown*)_device, 0,
                        [DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT],
                        duplication);
                }
            }
            catch (Exception)
            {
                _logger.LogError("Failed to reacquire duplication. Switching thread desktop");
                if (PInvoke.OpenInputDesktop(DESKTOP_CONTROL_FLAGS.DF_ALLOWOTHERACCOUNTHOOK, false,
                        DESKTOP_ACCESS_FLAGS.DESKTOP_SWITCHDESKTOP) is { } hDesk)
                {
                    PInvoke.SetThreadDesktop(hDesk);
                }
            }
        }
    }

    private static void Initialize(uint gpuIndex, uint monitorIndex, out IDXGIAdapter4* adapter,
        out IDXGIOutput6* output,
        out uint screenWidth, out uint screenHeight)
    {
        PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED);
        PInvoke.SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        PInvoke.CreateDXGIFactory2(0, typeof(IDXGIFactory7).GUID, out var factoryPtr);
        var factory = (IDXGIFactory7*)factoryPtr;

        factory->EnumAdapterByGpuPreference(gpuIndex, DXGI_GPU_PREFERENCE.DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE,
            typeof(IDXGIAdapter4).GUID, out var adapterPtr);
        adapter = (IDXGIAdapter4*)adapterPtr;

        IDXGIOutput* outputPtr;
        adapter->EnumOutputs(monitorIndex, &outputPtr).ThrowOnFailure();
        output = QueryInterface<IDXGIOutput6>((IUnknown*)outputPtr);

        var outputDesc = output->GetDesc();
        screenWidth = (uint)outputDesc.DesktopCoordinates.Width;
        screenHeight = (uint)outputDesc.DesktopCoordinates.Height;
        factory->Release();
    }

    private void CreateDeviceResources(ID3D11Device4** pDevice, ID3D11DeviceContext4** pContext)
    {
        D3D_FEATURE_LEVEL[] featureLevels =
        [
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1,
        ];

        PInvoke.D3D11CreateDevice(
            (IDXGIAdapter*)_adapter,
            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_UNKNOWN,
            HMODULE.Null,
            D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            featureLevels,
            7,
            (ID3D11Device**)pDevice,
            null,
            (ID3D11DeviceContext**)pContext
        );


        var dxgiDevice = QueryInterface<IDXGIDevice4>((IUnknown*)*pDevice);
        dxgiDevice->SetMaximumFrameLatency(1);
        dxgiDevice->SetGPUThreadPriority(7);
    }

    private static ID3D11Buffer* CreateCaptureRegionBuffer(ID3D11Device4* device, uint captureWidth, uint captureHeight)
    {
        var captureRegion = new CaptureRegion
        {
            CaptureSize = new Int2((int)captureWidth, (int)captureHeight)
        };

        var bufferDesc = new D3D11_BUFFER_DESC
        {
            ByteWidth = (uint)Marshal.SizeOf<CaptureRegion>(),
            Usage = D3D11_USAGE.D3D11_USAGE_DYNAMIC,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
            CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE
        };

        var subresourceData = new D3D11_SUBRESOURCE_DATA
        {
            pSysMem = Unsafe.AsPointer(ref captureRegion)
        };

        ID3D11Buffer* pBuffer;
        device->CreateBuffer(&bufferDesc, &subresourceData, &pBuffer);

        return pBuffer;
    }

    private static ID3D11Buffer* CreateOutputBuffer(ID3D11Device4* device, uint captureWidth, uint captureHeight)
    {
        var bufferDesc = new D3D11_BUFFER_DESC
        {
            ByteWidth = captureWidth * captureHeight * 3 * sizeof(float),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_UNORDERED_ACCESS | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
            CPUAccessFlags = 0
        };

        ID3D11Buffer* pBuffer;
        device->CreateBuffer(&bufferDesc, null, &pBuffer);

        return pBuffer;
    }

    private static ID3D11Texture2D* CreateCapturedFrameTexture(ID3D11Device4* device, uint captureWidth,
        uint captureHeight)
    {
        var textureDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = captureWidth,
            Height = captureHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC
            {
                Count = 1,
                Quality = 0
            },
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
            CPUAccessFlags = 0,
            MiscFlags = 0
        };

        ID3D11Texture2D* pTexture;
        device->CreateTexture2D(&textureDesc, null, &pTexture);

        return pTexture;
    }

    private static ID3D11ComputeShader* CreateComputeShader(ID3D11Device4* device)
    {
        const string shaderCode = @"
    cbuffer CaptureRegion : register(b0)
    {
        int2 captureSize;
    };

    RWStructuredBuffer<float> output : register(u0);

    Texture2D<float4> inputTexture : register(t0);

    [numthreads(8, 8, 1)]
    void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
    {
        if (dispatchThreadID.x >= captureSize.x || dispatchThreadID.y >= captureSize.y)
        {
            return;
        }

        int2 texCoord = dispatchThreadID.xy;
        float4 color = inputTexture[texCoord];

        int width = captureSize.x;
        int height = captureSize.y;

        int bufferIndexR = dispatchThreadID.y * width + dispatchThreadID.x;
        int bufferIndexG = height * width + bufferIndexR;
        int bufferIndexB = 2 * height * width + bufferIndexR;

        output[bufferIndexR] = color.r;
        output[bufferIndexG] = color.g;
        output[bufferIndexB] = color.b;
    }
";
        var compiledShader = CompileShader(shaderCode);

        ID3D11ComputeShader* pComputeShader;
        device->CreateComputeShader(compiledShader->GetBufferPointer(), compiledShader->GetBufferSize(), null,
            &pComputeShader);

        return pComputeShader;
    }

    private static ID3D11UnorderedAccessView* CreateUav(ID3D11Device4* device, ID3D11Buffer* buffer, uint captureWidth,
        uint captureHeight)
    {
        var uavDesc = new D3D11_UNORDERED_ACCESS_VIEW_DESC
        {
            Format = DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT,
            ViewDimension = D3D11_UAV_DIMENSION.D3D11_UAV_DIMENSION_BUFFER,
            Anonymous = new D3D11_UNORDERED_ACCESS_VIEW_DESC._Anonymous_e__Union
            {
                Buffer = new D3D11_BUFFER_UAV
                {
                    FirstElement = 0,
                    NumElements = captureWidth * captureHeight * 3
                }
            }
        };

        ID3D11UnorderedAccessView* pUav;
        device->CreateUnorderedAccessView(QueryInterface<ID3D11Resource>((IUnknown*)buffer), &uavDesc, &pUav);

        return pUav;
    }

    private static ID3D11Buffer* CreateStagingBuffer(ID3D11Device4* device, uint captureWidth, uint captureHeight)
    {
        var bufferDesc = new D3D11_BUFFER_DESC
        {
            ByteWidth = captureWidth * captureHeight * 3 * sizeof(float),
            Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ | D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE
        };

        ID3D11Buffer* pBuffer;
        device->CreateBuffer(&bufferDesc, null, &pBuffer);

        return pBuffer;
    }


    private static ID3DBlob* CompileShader(string shaderCode)
    {
        ID3DBlob* pCompiledShader;
        var cStrShaderCode = Marshal.StringToHGlobalAnsi(shaderCode);
        PInvoke.D3DCompile(
            (void*)cStrShaderCode,
            (uint)shaderCode.Length,
            "RgbTensorConvertor",
            null,
            null,
            "CSMain",
            "cs_5_0",
            3,
            0,
            &pCompiledShader,
            null
        );

        return pCompiledShader;
    }


    // _duplication->AcquireNextFrame(0, out _, &desktopResource);
    // Because CsWin32 throws an exception when there's no new frame available in the span of the timeout. We don't want exceptions to be thrown for a normal case like this.
    // that's part of a hot path. So we need to use the native API directly.
    private static HRESULT AcquireNextFrame(uint timeout, IDXGIOutputDuplication* duplication,
        out DXGI_OUTDUPL_FRAME_INFO frameInfo,
        out IDXGIResource* desktopResource)
    {
        DXGI_OUTDUPL_FRAME_INFO frameInfoLocal;
        IDXGIResource* pDesktopResource;
        var lpVtbl = *(void***)duplication;
        var hr =
            ((delegate* unmanaged[Stdcall]<IDXGIOutputDuplication*, uint, DXGI_OUTDUPL_FRAME_INFO*, IDXGIResource**,
                HRESULT>)lpVtbl[8])(duplication, timeout, &frameInfoLocal, &pDesktopResource);
        frameInfo = frameInfoLocal;
        desktopResource = pDesktopResource;
        return hr;
    }


    private static T* QueryInterface<T>(IUnknown* pUnknown) where T : unmanaged
    {
        pUnknown->QueryInterface(typeof(T).GUID, out var pInterface).ThrowOnFailure();
        return (T*)pInterface;
    }
}