using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Win32;

namespace FpsAim;

public class InferenceEngine : IDisposable
{
    private const int MaxDetections = 10_000;


    protected readonly DetectionResult[] DetectionsBuffer = new DetectionResult[MaxDetections];
    protected readonly int InputHeight;
    protected readonly int InputWidth;
    protected readonly InferenceSession Session;

    public InferenceEngine(string modelPath, SessionOptions sessionOptions)
    {
        Session = new InferenceSession(modelPath, sessionOptions);
        var inputMeta = Session.InputMetadata;
        Debug.Assert(inputMeta.Count == 1);

        var firstInput = inputMeta.First();
        InputWidth = firstInput.Value.Dimensions[3];
        InputHeight = firstInput.Value.Dimensions[2];
    }


    void IDisposable.Dispose()
    {
        Session.Dispose();
    }


    protected static unsafe void ProcessImageFromBGRAInto_U8RGB(ReadOnlySpan<byte> input, DenseTensor<byte> inputTensor,
        int width,
        int height)
    {
        if (!Avx2.IsSupported) throw new PlatformNotSupportedException("AVX2 is not supported on this platform.");

        var vectorSize = Vector256<byte>.Count;
        var pixelsPerVector = vectorSize / 4;

        if (width % pixelsPerVector != 0) throw new ArgumentException("Width must be a multiple of 8.");

        var pInput = input.GetPointer();
        if (pInput == null) throw new ArgumentNullException(nameof(pInput));

        Parallel.For(0, height, y =>
        {
            var inputRowStart = pInput + y * width * 4;
            var x = 0;

            for (; x <= width - pixelsPerVector; x += pixelsPerVector)
            {
                var inputVector = Avx.LoadVector256(inputRowStart + x * 4);

                // Extract and store R, G, B values
                for (var i = 0; i < pixelsPerVector; i++)
                {
                    inputTensor[0, 0, y, x + i] = inputVector.GetElement(i * 4 + 2);
                    inputTensor[0, 1, y, x + i] = inputVector.GetElement(i * 4 + 1);
                    inputTensor[0, 2, y, x + i] = inputVector.GetElement(i * 4);
                }
            }

            // Handle remaining pixels
            for (; x < width; x++)
            {
                var inputPixelIndex = x * 4;
                var r = inputRowStart[inputPixelIndex + 2];
                var g = inputRowStart[inputPixelIndex + 1];
                var b = inputRowStart[inputPixelIndex];

                inputTensor[0, 0, y, x] = r;
                inputTensor[0, 1, y, x] = g;
                inputTensor[0, 2, y, x] = b;
            }
        });
    }

    protected static unsafe void ProcessImageFromBGRAInto_F32RGB(ReadOnlySpan<byte> input,
        DenseTensor<float> inputTensor,
        int width,
        int height)
    {
        var pInput = input.GetPointer();
        if (pInput == null) throw new ArgumentNullException(nameof(pInput));

        Parallel.For(0, height, y =>
        {
            var inputRowStart = pInput + y * width * 4;
            var x = 0;

            for (; x < width; x++)
            {
                var inputPixelIndex = x * 4;
                var r = inputRowStart[inputPixelIndex + 2];
                var g = inputRowStart[inputPixelIndex + 1];
                var b = inputRowStart[inputPixelIndex];

                inputTensor[0, 0, y, x] = r / 255.0f;
                inputTensor[0, 1, y, x] = g / 255.0f;
                inputTensor[0, 2, y, x] = b / 255.0f;
            }
        });
    }

    public virtual void Infer(ReadOnlySpan<byte> input, int width, int height, float confidenceThreshold)
    {
        Debug.Assert(width == InputWidth && height == InputHeight,
            "Input dimensions do not match the model input dimensions.");
        Array.Clear(DetectionsBuffer, 0, MaxDetections);
    }

    public IEnumerable<DetectionResult> GetBestDetections()
    {
        var bestScores = new Dictionary<int, float>();
        var bestResults = new Dictionary<int, DetectionResult>();

        foreach (var detection in DetectionsBuffer)
        {
            var classId = (int)detection.ClassId;
            if (!(detection.Confidence > bestScores.GetValueOrDefault(classId, 0))) continue;

            bestScores[classId] = detection.Confidence;
            bestResults[classId] = detection;
        }

        return bestResults.Values;
    }

    public (float, float) ScreenCoords(float x, float y, int screenWidth, int screenHeight)
    {
        var scxr = screenWidth / (float)InputWidth;
        var scyr = screenHeight / (float)InputHeight;
        return (x * scxr, y * scyr);
    }
}

public readonly record struct DetectionResult
{
    public required long ClassId { get; init; }
    public required float Confidence { get; init; }
    public required float XMin { get; init; }
    public required float YMin { get; init; }
    public required float XMax { get; init; }
    public required float YMax { get; init; }
    public required float Width { get; init; }
    public required float Height { get; init; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float GetDistanceUnits(float x1, float y1, float x2, float y2)
    {
        var width = XMax - XMin;
        var height = YMax - YMin;
        var dx = x2 - x1;
        var dy = y2 - y1;
        var dxNorm = dx / width;
        var dyNorm = dy / height;

        return MathF.Sqrt(dxNorm * dxNorm + dyNorm * dyNorm);
    }
}