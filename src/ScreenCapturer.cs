using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using ResultCode = Vortice.DXGI.ResultCode;

namespace FpsAim
{
    public class ScreenCapturer : IDisposable
    {
        private static readonly FeatureLevel[] FeatureLevels =
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
        };

        private readonly IDXGIAdapter1 _adapter;
        private readonly int _captureHeight;
        private readonly int _captureWidth;
        private readonly ID3D11DeviceContext _context;
        private readonly ID3D11Device _d3D11Device;
        private IDXGIOutputDuplication _duplication;
        private readonly IDXGIFactory1 _factory;
        private readonly IDXGIOutput1 _output;
        private readonly ID3D11ComputeShader _computeShader;
        private readonly ID3D11Buffer _outputBuffer;

        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private ID3D11Buffer _captureRegionBuffer;

        private readonly ID3D11UnorderedAccessView _outputUav;
        private readonly ID3D11Buffer _stagingBuffer;

        private const string ShaderCode = @"
cbuffer CaptureRegion : register(b0)
{
    int2 offset;
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

    int2 texCoord = dispatchThreadID.xy + offset;
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

        private readonly ThreadLocal<float[]> _backingArray;

        public int MonitorIndex { get; }

        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

        public ScreenCapturer(int monitorIndex, int captureWidth, int captureHeight)
        {
            MonitorIndex = monitorIndex;
            _captureWidth = captureWidth;
            _captureHeight = captureHeight;
            _backingArray = new ThreadLocal<float[]>(() => new float[_captureWidth * _captureHeight * 3]);

            Initialize(out _factory, out _adapter, out _d3D11Device, out _context, out _output,
                out _duplication, out _computeShader, out _outputBuffer, out _outputUav, out _captureRegionBuffer,
                out _stagingBuffer);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _outputUav.Dispose();
            _stagingBuffer.Dispose();
            _duplication.Dispose();
            _context.Dispose();
            _d3D11Device.Dispose();
            _output.Dispose();
            _adapter.Dispose();
            _factory.Dispose();
        }

        private void Initialize(out IDXGIFactory1 factory, out IDXGIAdapter1 adapter,
            out ID3D11Device device, out ID3D11DeviceContext context, out IDXGIOutput1 output,
            out IDXGIOutputDuplication duplication, out ID3D11ComputeShader computeShader,
            out ID3D11Buffer outputBuffer, out ID3D11UnorderedAccessView outputUav,
            out ID3D11Buffer captureRegionBuffer, out ID3D11Buffer stagingBuffer)
        {
            factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            factory.EnumAdapters1(0, out adapter);
            adapter.EnumOutputs(MonitorIndex, out var outputGeneric);
            output = outputGeneric.QueryInterface<IDXGIOutput1>();
            D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.None, FeatureLevels, out device,
                out context);
            duplication = output.DuplicateOutput(device);

            var desc = output.Description;
            ScreenWidth = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
            ScreenHeight = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;

            computeShader = device.CreateComputeShader(Compiler
                .Compile(ShaderCode, "CSMain", "RgbTensorConvertor", "cs_5_0",
                    ShaderFlags.OptimizationLevel3)
                .Span);

            context.CSSetShader(_computeShader, []);

            outputBuffer = device.CreateBuffer(
                new BufferDescription(
                    _captureWidth * _captureHeight * 3 * sizeof(float),
                    BindFlags.UnorderedAccess | BindFlags.ShaderResource
                )
            );

            outputUav = device.CreateUnorderedAccessView(outputBuffer,
                new UnorderedAccessViewDescription
                {
                    Format = Format.R32_Float,
                    ViewDimension = UnorderedAccessViewDimension.Buffer,
                    Buffer = new BufferUnorderedAccessView
                    {
                        FirstElement = 0,
                        NumElements = _captureWidth * _captureHeight * 3
                    }
                }
            );

            stagingBuffer = device.CreateBuffer(new BufferDescription(
                _captureWidth * _captureHeight * 3 * sizeof(float),
                BindFlags.None,
                ResourceUsage.Staging,
                CpuAccessFlags.Read
            ));

            unsafe
            {
                var offsetX = (ScreenWidth - _captureWidth) / 2;
                var offsetY = (ScreenHeight - _captureHeight) / 2;
                var captureRegion = new CaptureRegion
                {
                    Offset = new Int2(offsetX, offsetY),
                    CaptureSize = new Int2(_captureWidth, _captureHeight)
                };

                captureRegionBuffer = device.CreateBuffer(
                    new BufferDescription(
                        Marshal.SizeOf<CaptureRegion>(),
                        BindFlags.ConstantBuffer,
                        ResourceUsage.Dynamic,
                        CpuAccessFlags.Write
                    ),
                    new SubresourceData(&captureRegion)
                );

                context.CSSetConstantBuffers(0, [captureRegionBuffer]);
                context.CSSetUnorderedAccessViews(0, [_outputUav]);
            }
        }

        public Memory<float>? CaptureFrame()
        {
            var result = _duplication.AcquireNextFrame(1, out _, out var resource);

            if (result.Failure) return default;

            if (result.Code == ResultCode.AccessLost)
            {
                _duplication.Release();
                _duplication = _output.DuplicateOutput(_d3D11Device);
                return default;
            }

            using var screenTexture = resource.QueryInterface<ID3D11Texture2D>();

            using var textureSrv = _d3D11Device.CreateShaderResourceView(screenTexture);
            _context.CSSetShaderResources(0, [textureSrv]);

            var dispatchX = (int)Math.Ceiling(_captureWidth / 8.0);
            var dispatchY = (int)Math.Ceiling(_captureHeight / 8.0);
            _context.Dispatch(dispatchX, dispatchY, 1);

            _context.CSSetShaderResources(0, []);

            _context.CopyResource(_stagingBuffer, _outputBuffer);

            var mappedResource = _context.Map(_stagingBuffer, 0, MapMode.Read);

            var backingArray = _backingArray.Value!;
            var output = new Memory<float>(backingArray);
            mappedResource.AsSpan<float>(_captureWidth * _captureHeight * 3).CopyTo(output.Span);
            _context.Unmap(_stagingBuffer, 0);

            resource.Dispose();
            _duplication.ReleaseFrame();

            return output;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CaptureRegion
    {
        public Int2 Offset;
        public Int2 CaptureSize;
    }
}