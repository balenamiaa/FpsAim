using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using ResultCode = Vortice.DXGI.ResultCode;

namespace FpsAim;

public class ScreenCapturer : IDisposable
{
    private static readonly FeatureLevel[] FeatureLevels =
    {
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0,
        FeatureLevel.Level_9_3,
        FeatureLevel.Level_9_2,
        FeatureLevel.Level_9_1
    };

    private static readonly ThreadLocal<byte[]> ThreadLocalBuffer = new(() => new byte[640 * 640 * 4]);
    private IDXGIAdapter1? _adapter;
    private int _captureHeight;
    private int _captureWidth;
    private ID3D11DeviceContext? _context;
    private ID3D11Device? _device;
    private IDXGIOutputDuplication? _duplication;
    private IDXGIFactory1? _factory;
    private IDXGIOutput1? _output;
    private ID3D11Texture2D? _stagingTexture;

    public ScreenCapturer(int monitorIndex)
    {
        MonitorIndex = monitorIndex;
        Initialize();
    }

    public int MonitorIndex { get; }

    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _duplication?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
        _output?.Dispose();
        _adapter?.Dispose();
        _factory?.Dispose();
        _stagingTexture?.Dispose();
    }

    private void Initialize(int captureWidth = 640, int captureHeight = 640)
    {
        Debug.Assert(captureWidth <= 640 && captureHeight <= 640, "Capture dimensions exceed the 640x640 limit.");

        _factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        _factory.EnumAdapters1(0, out _adapter);
        _adapter.EnumOutputs(MonitorIndex, out var outputGeneric);
        _output = outputGeneric.QueryInterface<IDXGIOutput1>();
        D3D11.D3D11CreateDevice(_adapter, DriverType.Unknown, DeviceCreationFlags.None, FeatureLevels, out _device,
            out _context);
        _duplication = _output.DuplicateOutput(_device);

        var stagingTextureDesc = new Texture2DDescription
        {
            Width = captureWidth,
            Height = captureHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read
        };

        _stagingTexture = _device!.CreateTexture2D(stagingTextureDesc);

        var desc = _output.Description;
        ScreenWidth = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
        ScreenHeight = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;
        _captureWidth = captureWidth;
        _captureHeight = captureHeight;
    }

    public unsafe Span<byte> CaptureFrame()
    {
        var result = _duplication!.AcquireNextFrame(1, out _, out var resource);

        if (result.Failure) return [];

        if (result.Code == ResultCode.AccessLost)
        {
            _duplication.Release();
            _duplication = _output!.DuplicateOutput(_device);
            return [];
        }

        using var screenTexture = resource.QueryInterface<ID3D11Texture2D>();
        var textureDesc = screenTexture.Description;


        var offsetX = (textureDesc.Width - _captureWidth) / 2;
        var offsetY = (textureDesc.Height - _captureHeight) / 2;

        _context!.CopySubresourceRegion(_stagingTexture!, 0, 0, 0, 0, screenTexture, 0,
            new Box(offsetX, offsetY, 0, offsetX + _captureWidth, offsetY + _captureHeight, 1));

        var dataBox = _context.Map(_stagingTexture!, 0);

        var data = ThreadLocalBuffer.Value!;
        var byteSize = _captureWidth * _captureHeight * 4;

        var sourcePtr = (byte*)dataBox.DataPointer;

        // Parallel.For(0, _captureHeight, y =>
        // {
        //     var sourceRowStart = sourcePtr + y * dataBox.RowPitch;
        //     Marshal.Copy((IntPtr)sourceRowStart, data, y * _captureWidth * 4, _captureWidth * 4);
        // });
        for (var y = 0; y < _captureHeight; y++)
        {
            var sourceRowStart = sourcePtr + y * dataBox.RowPitch;
            Marshal.Copy((IntPtr)sourceRowStart, data, y * _captureWidth * 4, _captureWidth * 4);
        }

        _context.Unmap(_stagingTexture, 0);
        _duplication.ReleaseFrame();

        return data.AsSpan(0, byteSize);
    }
}