using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;

namespace FpsAim;

public enum AimAssistTarget
{
    Head,
    Torso
}

public enum Executer
{
    Cpu,
    Cuda,
    TensorRT
}

public readonly record struct AimAssistConfiguration
{
    public required InferenceEngine Engine { get; init; }
    public required ITargetPredictor TargetPredictor { get; init; }
    public required ISmoothingFunction SmoothingFunction { get; init; }
    public required IAimAssistCondition ActivationCondition { get; init; }
    public required AimAssistTarget Target { get; init; }
    public required float ConfidenceThreshold { get; init; }
    public required int CaptureWidth { get; init; }
    public required int CaptureHeight { get; init; }
    public required float XCorrectionMultiplier { get; init; }
    public required float YCorrectionMultiplier { get; init; }

    public static SessionOptions GetSessionOptions(Executer executer)
    {
        var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        return executer switch
        {
            Executer.Cpu => options,
            Executer.Cuda => Apply(o => o.AppendExecutionProvider_CUDA()),
            Executer.TensorRT => Apply(o => o.AppendExecutionProvider_Tensorrt()),
            _ => throw new ArgumentOutOfRangeException()
        };

        SessionOptions Apply(Action<SessionOptions> f)
        {
            f(options);
            return options;
        }
    }
}

public class AimAssistModule(AimAssistConfiguration configuration)
{
    public void Run()
    {
        using var screenCapturer = new ScreenCapturer(0, configuration.CaptureWidth, configuration.CaptureHeight);
        using var mouseMover = new MouseMover();

        var classToTarget = configuration.Target switch
        {
            AimAssistTarget.Head => 0,
            AimAssistTarget.Torso => 1,
            _ => throw new ArgumentOutOfRangeException()
        };

        var screenWidth = screenCapturer.ScreenWidth;
        var screenHeight = screenCapturer.ScreenHeight;
        var screenCenterX = screenWidth / 2;
        var screenCenterY = screenHeight / 2;

        Warmup(screenCapturer);

        Console.WriteLine("Aim assist is running. Press Ctrl+C to exit.");

        var frames = 0;
        var totalTimeMs = 0L;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            configuration.ActivationCondition.Update();
            var frame = screenCapturer.CaptureFrame();
            if (frame.Length == 0) continue;

            configuration.Engine.Infer(frame, configuration.CaptureWidth, configuration.CaptureHeight,
                configuration.ConfidenceThreshold);

            var detections = NonMaximumSuppression.Applied(configuration.Engine.GetBestDetections(), 0.5f);

            var detectionResult = GetClosestToCenter(detections.Where(d => d.ClassId == classToTarget));

            if (detectionResult is { } detection)
            {
                var targetX = (detection.XMin + detection.XMax) / 2.0f;
                var targetY = (detection.YMin + detection.YMax) / 2.0f;
                var (predictedX, predictedY) =
                    configuration.TargetPredictor.Predict(targetX, targetY,
                        stopwatch.Elapsed.TotalSeconds); // We predict regardless to build the model.

                if (configuration.ActivationCondition.ShouldAimAssist())
                {
                    var (x, y) =
                        detection.OffsetAbsoluteFromCenter(predictedX, predictedY, screenCenterX, screenCenterY);
                    var distance = detection.GetDistanceUnits(x, y, screenCenterX, screenCenterY);

                    var dx = configuration.XCorrectionMultiplier * (x - screenCenterX);
                    var dy = configuration.YCorrectionMultiplier * (y - screenCenterY);

                    var smoothingFactor = configuration.SmoothingFunction.Calculate(distance);
                    var dxSmoothed = (int)Math.Ceiling(dx * smoothingFactor);
                    var dySmoothed = (int)Math.Ceiling(dy * smoothingFactor);
                    mouseMover.MoveRelative(dxSmoothed, dySmoothed);
                }
            }

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            totalTimeMs += elapsedMs;
            frames++;

            if (frames % 100 == 0) Console.WriteLine($"Total time: {totalTimeMs / (float)frames} ms");

            stopwatch.Restart();
        }
    }


    private static DetectionResult? GetClosestToCenter(
        IEnumerable<DetectionResult> detections)
    {
        DetectionResult? best = null;
        var bestDistance = float.MaxValue;

        foreach (var detection in detections)
        {
            var centerX = (detection.XMin + detection.XMax) / 2.0f;
            var centerY = (detection.YMin + detection.YMax) / 2.0f;
            var distance = detection.GetDistanceUnits(centerX, centerY,
                detection.Width / 2,
                detection.Height / 2
            );

            switch (best)
            {
                case not null when distance < bestDistance:
                    best = detection;
                    bestDistance = distance;
                    break;
                case null:
                    best = detection;
                    break;
            }
        }

        return best;
    }


    private void Warmup(ScreenCapturer screenCapturer, int warmupFrames = 10)
    {
        for (var i = 0; i < warmupFrames; i++)
        {
            var frame = screenCapturer.CaptureFrame();
            if (frame.Length == 0) continue;
            configuration.Engine.Infer(frame, configuration.CaptureWidth, configuration.CaptureHeight, 0.1f);
        }
    }
}