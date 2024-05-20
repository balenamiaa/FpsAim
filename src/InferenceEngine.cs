using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.ML.OnnxRuntime;

namespace FpsAim;

public class InferenceEngine : IDisposable
{
    public const int MaxDetections = 10_000;


    protected readonly DetectionResult[] DetectionsBuffer = new DetectionResult[MaxDetections];
    protected readonly int InputHeight;
    protected readonly int InputWidth;
    protected readonly InferenceSession Session;

    protected InferenceEngine(string modelPath, SessionOptions sessionOptions)
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

    public virtual void Infer(Memory<float> input, int width, int height,
        float confidenceThreshold)
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

    public (float, float) OffsetRelativeFromCenter(float xNorm, float yNorm, float centerX, float centerY)
    {
        var top = centerY - Height / 2;
        var left = centerX - Width / 2;

        return (left + xNorm * Width, top + yNorm * Height);
    }

    public (float, float) OffsetAbsoluteFromCenter(float x, float y, float centerX, float centerY)
    {
        var top = centerY - Height / 2;
        var left = centerX - Width / 2;

        return (left + x, top + y);
    }
}