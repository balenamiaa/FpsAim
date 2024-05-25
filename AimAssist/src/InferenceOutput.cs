namespace AimAssist;

public record struct InferenceOutput(DetectionResult[] Detections, double InferenceTimeMs, double ScreenCaptureTimeMs);
