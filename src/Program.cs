using System.Diagnostics;
using FpsAim;

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

    private static void RunAimAssist()
    {
        var config = new AimAssistConfig
        {
            ConfidenceThreshold = 0.5f,
            Target = AimAssistTarget.Head,
            Executer = Executer.TensorRT
        };
        var predictor = new KalmanFilterPredictor();
        var smoothingFunction = new LogarithmicSmoothing(0.6f);
        AimAssistModule.Run(predictor, smoothingFunction, () => !MouseMover.IsMouse5Down(), config);
    }

    public static void Main(string[] args)
    {
        RunAimAssist();
    }
}