using Microsoft.ML.OnnxRuntime;

namespace AimAssist;

public struct AimAssistConfiguration
{
    public InferenceSession Engine { get; set; }
    public ITargetPredictor TargetPredictor { get; set; }
    public ISmoothingFunction SmoothingFunction { get; set; }
    public IActivationCondition ActivationCondition { get; set; }
    public AimAssistTarget[] Targets { get; set; }
    public float ConfidenceThreshold { get; set; }
    public uint CaptureWidth { get; set; }
    public uint CaptureHeight { get; set; }
    public float XCorrectionMultiplier { get; set; }
    public float YCorrectionMultiplier { get; set; }

    public static AimAssistConfiguration GetCs2Configuration()
    {
        StickySmoothing.Breakpoint[] breakpoints = [
            new() { Distance = 0.0f, Value = 0.0f },
            new() { Distance = 0.1f, Value = 0.24f },
            new() { Distance = 0.65f, Value = 0.08f },
            new() { Distance = 1.0f, Value = 0.01f },
            new() { Distance = 2f, Value = 0.005f },
            new() { Distance = 5.0f, Value = 0.005f },
            new() { Distance = 100.0f, Value = 0.00001f }
        ];

        return new AimAssistConfiguration
        {
            Engine = CreateAimAssistSession("v8-nn.onnx", AimAssistAccelerator.CUDA),
            TargetPredictor = new NullPredictor(),
            // SmoothingFunction = new ProgressiveSmoothing(new SigmoidSmoothing(2.0f, 0.10f, 0.18f, 2.5f), 4.0f),
            SmoothingFunction = new ProgressiveSmoothing(new StickySmoothing(breakpoints), 4.0f),
            ActivationCondition = new ToggleActivationCondition(MouseButton.XButton1),
            Targets = [AimAssistTarget.Head],
            ConfidenceThreshold = 0.6f,
            CaptureWidth = 320,
            CaptureHeight = 320,
            XCorrectionMultiplier = 22.0f * (640.0f / 1280.0f),
            YCorrectionMultiplier = 22.0f / 1.2f * (640.0f / 1280.0f)
        };
    }

    private static InferenceSession CreateAimAssistSession(string modelPath, AimAssistAccelerator accelerator)
    {
        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        switch (accelerator)
        {
            case AimAssistAccelerator.TensorRT:
                sessionOptions.AppendExecutionProvider_Tensorrt(0);
                break;
            case AimAssistAccelerator.CUDA:
                sessionOptions.AppendExecutionProvider_CUDA(0);
                break;
        }

        return new InferenceSession(modelPath, sessionOptions);
    }
}
