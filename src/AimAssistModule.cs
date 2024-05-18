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

public record struct AimAssistConfig(
    Executer Executer,
    AimAssistTarget Target,
    float ConfidenceThreshold)
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
    public static void Run(ITargetPredictor predictor, ISmoothingFunction smoothingFunction,
        Func<bool> activationCriteria, AimAssistConfig config)
    {
        using var screenCapturer = new ScreenCapturer(0);
        using var engine = new YoloNASEngine("model.onnx", config.GetSessionOptions());
        using var mouseMover = new MouseMover();

        var classToTarget = config.Target switch
        {
            AimAssistTarget.Head => 0,
            AimAssistTarget.Torso => 1,
            _ => throw new ArgumentOutOfRangeException()
        };

        var screenWidth = screenCapturer.ScreenWidth;
        var screenHeight = screenCapturer.ScreenHeight;

        Warmup(screenCapturer, engine);

        Console.WriteLine("Aim assist is running. Press Ctrl+C to exit.");

        var frames = 0;
        var totalTimeMs = 0L;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var frame = screenCapturer.CaptureFrame();
            if (frame.Length == 0) continue;

            engine.InferScreenCapture(frame, 640, 640);

            var detections = engine.GetBestDetections().ToList();
            detections = NonMaximumSuppression.Run(detections, 0.5f);

            var detectionResult = GetClosestToCenter(
                detections.Where(d =>
                    d.ClassId == classToTarget && d.Confidence >= config.ConfidenceThreshold),
                screenWidth,
                screenHeight);

            if (detectionResult is { } detection)
            {
                var targetX = (detection.XMin + detection.XMax) / 2.0f;
                var targetY = (detection.YMin + detection.YMax) / 2.0f;
                var (predictedX, predictedY) =
                    predictor.Predict(targetX, targetY,
                        stopwatch.Elapsed.TotalSeconds); // We predict regardless to build the model.
                if (activationCriteria())
                {
                    var (x, y) = YoloNASEngine.ScreenCoords(
                        predictedX,
                        predictedY,
                        screenWidth,
                        screenHeight);

                    var dx = x - screenWidth / 2.0f;
                    var dy = y - screenHeight / 2.0f;

                    var smoothingFactor = smoothingFunction.Calculate(dx, dy);
                    var dxSmoothed = (int)Math.Ceiling(dx * smoothingFactor);
                    var dySmoothed = (int)Math.Ceiling(dy * smoothingFactor);
                    mouseMover.MoveRelative(dxSmoothed, dySmoothed);
                }
            }

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            totalTimeMs += elapsedMs;
            frames++;

            if (frames % 100 == 0) Console.WriteLine($"Average time: {totalTimeMs / (float)frames} ms");

            stopwatch.Restart();
        }
    }

    private static DetectionResult? GetClosestToCenter(
        IEnumerable<DetectionResult> detections, int screenWidth, int screenHeight)
    {
        DetectionResult? best = null;
        var centerX = screenWidth / 2;
        var centerY = screenHeight / 2;

        foreach (var detection in detections)
        {
            var x = (int)(detection.XMin + detection.XMax) / 2;
            var y = (int)(detection.YMin + detection.YMax) / 2;

            var (xScreenCords, yScreenCords) = YoloNASEngine.ScreenCoords(
                x,
                y,
                screenWidth,
                screenHeight);

            var dx = xScreenCords - centerX;
            var dy = yScreenCords - centerY;

            switch (best)
            {
                case { } bestNotNull when dx * dx + dy * dy < (bestNotNull.XMin + bestNotNull.XMax) / 2:
                case null:
                    best = detection;
                    break;
            }
        }

        return best;
    }


    private static void Warmup(ScreenCapturer screenCapturer, YoloNASEngine engine)
    {
        for (var i = 0; i < 10; i++)
        {
            var frame = screenCapturer.CaptureFrame();
            if (frame.Length == 0) continue;
            engine.InferScreenCapture(frame, 640, 640);
        }
    }
}