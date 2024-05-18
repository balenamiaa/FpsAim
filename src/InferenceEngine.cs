using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Win32;

namespace FpsAim;

public class InferenceEngine(string modelPath, SessionOptions sessionOptions) : IDisposable
{
    private const int MaxDetections = 100;
    protected const int ImageWidth = 448;
    protected const int ImageHeight = 448;

    protected readonly DetectionResult[] DetectionsBuffer = new DetectionResult[MaxDetections];
    protected readonly InferenceSession Session = new(modelPath, sessionOptions);

    protected virtual string[] Classes => [];

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

    public virtual void Infer(ReadOnlySpan<byte> input, int width, int height)
    {
        if (width != ImageWidth || height != ImageHeight) throw new ArgumentException("Invalid input dimensions.");
        Array.Clear(DetectionsBuffer, 0, MaxDetections);
    }

    public IEnumerable<DetectionResult> GetBestDetections()
    {
        var bestScores = new float[Classes.Length];
        var bestResults = new DetectionResult?[Classes.Length];

        foreach (var detection in DetectionsBuffer.Where(d => d.Confidence > 0))
        {
            var classId = (int)detection.ClassId;
            if (!(detection.Confidence > bestScores[classId])) continue;

            bestScores[classId] = detection.Confidence;
            bestResults[classId] = detection;
        }

        return bestResults.Where(r => r != null).Select(result => result!.Value);
    }

    public static (float, float) ScreenCoords(float x, float y, int screenWidth, int screenHeight)
    {
        var scxr = screenWidth / (float)ImageWidth;
        var scyr = screenHeight / (float)ImageHeight;
        return (x * scxr, y * scyr);
    }
}

public record struct DetectionResult(long ClassId, float Confidence, float XMin, float YMin, float XMax, float YMax);