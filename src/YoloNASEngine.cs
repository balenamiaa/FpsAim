using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FpsAim;

// ReSharper disable once InconsistentNaming
public sealed unsafe class YoloNASEngine : IDisposable
{
    private const int MaxDetections = 100;
    private const int ImageWidth = 640;
    private const int ImageHeight = 640;
    private static readonly string[] Classes = ["enemy_head", "enemy_torso"];

    private static readonly Vector256<byte> MaskR = Vector256.Create(
        2, 6, 10, 14, 18, 22, 26, 30,
        255, 255, 255, 255, 255, 255, 255, 255,
        2, 6, 10, 14, 18, 22, 26, 30,
        255, 255, 255, 255, 255, 255, 255, 255);

    private static readonly Vector256<byte> MaskG = Vector256.Create(
        1, 5, 9, 13, 17, 21, 25, 29,
        255, 255, 255, 255, 255, 255, 255, 255,
        1, 5, 9, 13, 17, 21, 25, 29,
        255, 255, 255, 255, 255, 255, 255, 255);

    private static readonly Vector256<byte> MaskB = Vector256.Create(
        0, 4, 8, 12, 16, 20, 24, 28,
        255, 255, 255, 255, 255, 255, 255, 255,
        0, 4, 8, 12, 16, 20, 24, 28,
        255, 255, 255, 255, 255, 255, 255, 255);

    private static readonly Vector128<byte> MaskR128 = Vector128.Create(
        2, 6, 10, 14, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255);

    private static readonly Vector128<byte> MaskG128 = Vector128.Create(
        1, 5, 9, 13, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255
    );

    private static readonly Vector128<byte> MaskB128 = Vector128.Create(
        0, 4, 8, 12, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255
    );

    private readonly DetectionResult[] _detectionsBuffer = new DetectionResult[MaxDetections];

    private readonly DenseTensor<byte> _inputTensor;

    private readonly InferenceSession _session;

    public YoloNASEngine(string modelPath, SessionOptions sessionOptions)
    {
        _session = new InferenceSession(modelPath, sessionOptions);

        var inputMeta = _session.InputMetadata;
        Debug.Assert(inputMeta.Count == 1);
        Debug.Assert(inputMeta.First().Key == "input");
        Debug.Assert(inputMeta.First().Value.Dimensions.Take(4).SequenceEqual([1, 3, ImageWidth, ImageHeight]));
        _inputTensor = new DenseTensor<byte>([1, 3, ImageWidth, ImageHeight]);
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    public void InferScreenCapture(ReadOnlySpan<byte> input, int width, int height)
    {
        Array.Clear(_detectionsBuffer, 0, MaxDetections);
        if (width != ImageWidth || height != ImageHeight) throw new ArgumentException("Invalid input dimensions.");

        fixed (byte* pInput = input)
        {
            ProcessImage(pInput, _inputTensor, width, height);
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", _inputTensor)
        };


        using var outputs = _session.Run(inputs);
        ParseOutput(outputs);
    }

    private static void ProcessImage(byte* pInput, DenseTensor<byte> inputTensor, int width, int height)
    {
        if (!Avx2.IsSupported) throw new PlatformNotSupportedException("AVX2 is not supported on this platform.");

        var vectorSize = Vector256<byte>.Count;
        var pixelsPerVector = vectorSize / 4;

        if (width % pixelsPerVector != 0) throw new ArgumentException("Width must be a multiple of 8.");

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

    private void ParseOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output)
    {
        // Extract the number of predictions
        var numPredictions = (int)output.First(o => o.Name == "graph2_num_predictions")
            .AsTensor<long>().GetValue(0);

        // Extract bounding boxes
        var predBoxes = output.First(o => o.Name == "graph2_pred_boxes")
            .AsTensor<float>();

        // Extract scores
        var predScores = output.First(o => o.Name == "graph2_pred_scores")
            .AsTensor<float>();

        // Extract class IDs
        var predClasses = output.First(o => o.Name == "graph2_pred_classes")
            .AsTensor<long>();

        for (var i = 0; i < numPredictions; i++)
        {
            var xMin = predBoxes[0, i, 0];
            var yMin = predBoxes[0, i, 1];
            var xMax = predBoxes[0, i, 2];
            var yMax = predBoxes[0, i, 3];

            _detectionsBuffer[i] = new DetectionResult
            {
                ClassId = predClasses[0, i],
                Confidence = predScores[0, i],
                XMin = xMin,
                YMin = yMin,
                XMax = xMax,
                YMax = yMax
            };
        }
    }

    public IReadOnlyList<DetectionResult> GetBestDetections()
    {
        var bestScores = new float[Classes.Length];
        var bestResults = new DetectionResult?[Classes.Length];

        foreach (var detection in _detectionsBuffer.Where(d => d.Confidence > 0))
        {
            var classId = (int)detection.ClassId;
            if (detection.Confidence > bestScores[classId])
            {
                bestScores[classId] = detection.Confidence;
                bestResults[classId] = detection;
            }
        }

        return bestResults.Where(r => r != null).Select(result => result!.Value).ToList();
    }

    public static (float, float) ScreenCoords(float x, float y, int screenWidth, int screenHeight)
    {
        var scxr = screenWidth / (float)ImageWidth;
        var scyr = screenHeight / (float)ImageHeight;
        return (x * scxr, y * scyr);
    }
}

public record struct DetectionResult(long ClassId, float Confidence, float XMin, float YMin, float XMax, float YMax);