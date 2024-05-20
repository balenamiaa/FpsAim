using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace FpsAim;

public static class Program
{
    [SuppressMessage("ReSharper", "FunctionNeverReturns")]
    private static void BenchmarkScreenCapturer()
    {
        using var screenCapturer = new ScreenCapturer(0, 640, 640);


        while (true)
        {
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
        XCorrectionMultiplier = 22.0f * (640.0f / 1280.0f),
        YCorrectionMultiplier = 22.0f / 1.2f * (640.0f / 1280.0f),
        ActivationCondition = new ClickOnceActivationCondition(ClickOnceActivationCondition.ClickMouseButton.Mouse5)
    };

    private static AimAssistConfiguration TrackingConfiguration => new()
    {
        ConfidenceThreshold = 0.5f,
        Target = AimAssistTarget.Head,
        CaptureWidth = 448,
        CaptureHeight = 448,
        Engine = new YoloV8Engine("v8-n.onnx", AimAssistConfiguration.GetSessionOptions(Executer.TensorRT)),
        TargetPredictor = new LinearPredictor(),
        SmoothingFunction = new SigmoidSmoothing(1f, 0.6f, 1.6f, 3f),
        XCorrectionMultiplier = 22.0f * (640.0f / 1280.0f),
        YCorrectionMultiplier = 22.0f / 1.2f * (640.0f / 1280.0f),
        ActivationCondition = new KeyNotDownActivationCondition(KeyNotDownActivationCondition.MouseButton.LButton)
    };

    private static void RunAimAssist()
    {
        var aimAssistModule = new AimAssistModule(TrackingConfiguration);
        aimAssistModule.Run();
    }

    public static void Main(string[] args)
    {
        RunAimAssist();
    }
}