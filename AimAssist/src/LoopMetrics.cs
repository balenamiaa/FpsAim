namespace AimAssist;

internal class LoopMetrics
{
    private double _totalInferenceTimeMs;
    private double _totalScreenCaptureTimeMs;  
    
    public int LoopCount { get; private set; }

    public void Update(double inferenceTimeMs, double screenCaptureTimeMs)
    {
        _totalInferenceTimeMs += inferenceTimeMs;
        _totalScreenCaptureTimeMs += screenCaptureTimeMs;
        LoopCount++;
    }
    
    public void PrintAndReset()
    {
        Console.WriteLine($"Average inference time: {_totalInferenceTimeMs / LoopCount:F2}ms");  
        Console.WriteLine($"Average screen capture time: {_totalScreenCaptureTimeMs / LoopCount:F2}ms");

        _totalInferenceTimeMs = 0;
        _totalScreenCaptureTimeMs = 0;
        LoopCount = 0;
    }
}
