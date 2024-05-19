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

public readonly record struct AimAssistConfig(
    Executer Executer,
    AimAssistTarget Target,
    float ConfidenceThreshold,
    int CaptureWidth,
    int CaptureHeight)
{
    public SessionOptions GetSessionOptions()
    {
        var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        return Executer switch
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

public static class AimAssistModule
{
    public static void Run(InferenceEngine engine, ITargetPredictor predictor, ISmoothingFunction smoothingFunction,
        Func<bool> activationCriteria, AimAssistConfig config)
    {
        using var screenCapturer = new ScreenCapturer(0, config.CaptureWidth, config.CaptureHeight);
        using var mouseMover = new MouseMover();

        var classToTarget = config.Target switch
        {
            AimAssistTarget.Head => 0,
            AimAssistTarget.Torso => 1,
            _ => throw new ArgumentOutOfRangeException()
        };

        var screenWidth = screenCapturer.ScreenWidth;
        var screenHeight = screenCapturer.ScreenHeight;
        var screenCenterX = screenWidth / 2;
        var screenCenterY = screenHeight / 2;

        Warmup(screenCapturer, engine, config.CaptureWidth, config.CaptureHeight);

        Console.WriteLine("Aim assist is running. Press Ctrl+C to exit.");

        var frames = 0;
        var totalTimeMs = 0L;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var frame = screenCapturer.CaptureFrame();
            if (frame.Length == 0) continue;

            engine.Infer(frame, config.CaptureWidth, config.CaptureHeight, config.ConfidenceThreshold);

            var detections = engine.GetBestDetections().ToList();
            detections = NonMaximumSuppression.Run(detections, 0.5f);

            var detectionResult = GetClosestToCenter(detections.Where(d => d.ClassId == classToTarget));

            if (detectionResult is { } detection)
            {
                var targetX = (detection.XMin + detection.XMax) / 2.0f;
                var targetY = (detection.YMin + detection.YMax) / 2.0f;
                var (predictedX, predictedY) =
                    predictor.Predict(targetX, targetY,
                        stopwatch.Elapsed.TotalSeconds); // We predict regardless to build the model.

                if (activationCriteria())
                {
                    var (x, y) = engine.ScreenCoords(
                        predictedX,
                        predictedY,
                        screenWidth,
                        screenHeight);
                    var distance = detection.GetDistanceUnits(x, y, screenCenterX, screenCenterY);

                    var dx = x - screenCenterX;
                    var dy = y - screenCenterY;

                    var smoothingFactor = smoothingFunction.Calculate(distance);
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


    private static void Warmup(ScreenCapturer screenCapturer, InferenceEngine engine, int captureWidth,
        int captureHeight, int warmupFrames = 10)
    {
        for (var i = 0; i < warmupFrames; i++)
        {
            var frame = screenCapturer.CaptureFrame();
            if (frame.Length == 0) continue;
            engine.Infer(frame, captureWidth, captureHeight, 0.1f);
        }
    }
}