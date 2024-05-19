using System.Diagnostics;

namespace FpsAim;

public static class Program
{
    private static void BenchmarkScreenCapturer()
    {
        using var screenCapturer = new ScreenCapturer(0);

        var totalTimeMilliseconds = 0.0;
        for (var i = 0; i < 100; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            _ = screenCapturer.CaptureFrame();
            stopwatch.Stop();
            totalTimeMilliseconds += stopwatch.Elapsed.TotalMilliseconds;
        }

        Console.WriteLine($"Average time: {totalTimeMilliseconds / 100} ms");
    }

    private static AimAssistConfiguration FlickConfiguration => new()
    {
        ConfidenceThreshold = 0.5f,
        Target = AimAssistTarget.Head,
        CaptureWidth = 448,
        CaptureHeight = 448,
        Engine = new YoloV8Engine("v8-n.onnx", AimAssistConfiguration.GetSessionOptions(Executer.TensorRT)),
        TargetPredictor = new LinearPredictor(),
        SmoothingFunction = new NullSmoothing(),
        XCorrectionMultiplier = 22.0f,
        YCorrectionMultiplier = 22.0f / 1.2f,
        ActivationCondition = new ClickOnceActivationCondition(ClickOnceActivationCondition.ClickMouseButton.Mouse5)
    };

    private static AimAssistConfiguration TrackingConfiguration => new()
    {
        ConfidenceThreshold = 0.5f,
        Target = AimAssistTarget.Head,
        CaptureWidth = 448,
        CaptureHeight = 448,
        Engine = new YoloV8Engine("v8-n.onnx", AimAssistConfiguration.GetSessionOptions(Executer.TensorRT)),
        TargetPredictor = new NullPredictor(),
        SmoothingFunction = new SigmoidSmoothing(1f, 2.4f, 1.6f, 2f),
        XCorrectionMultiplier = 640.0f,
        YCorrectionMultiplier = 22.0f / 1.2f,
        ActivationCondition = new ToggleActivationCondition(ToggleActivationCondition.ToggleMouseButton.Mouse4)
    };

    private static void RunAimAssist()
    {
        var aimAssistModule = new AimAssistModule(FlickConfiguration);
        aimAssistModule.Run();
    }

    public static void Main(string[] args)
    {
        RunAimAssist();
    }
}