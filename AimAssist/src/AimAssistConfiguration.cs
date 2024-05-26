using Microsoft.ML.OnnxRuntime;

namespace AimAssist;

public struct AimAssistConfiguration
{
    public delegate FpsAim.Shared.AimAssistSettings GetDataForCommunicationFunc(AimAssistConfiguration config);
    public delegate void SetDataFromCommunicationFunc(AimAssistConfiguration config, FpsAim.Shared.AimAssistSettings settings);

    public required InferenceSession Engine { get; set; }
    public required ITargetPredictor TargetPredictor { get; set; }
    public required ISmoothingFunction SmoothingFunction { get; set; }
    public required IActivationCondition ActivationCondition { get; set; }
    public required ITargetPointSelector TargetPointSelector { get; set; }
    public required AimAssistTarget[] Targets { get; set; }
    public required float ConfidenceThreshold { get; set; }
    public required uint CaptureWidth { get; set; }
    public required uint CaptureHeight { get; set; }
    public required GetDataForCommunicationFunc GetDataForCommunication { get; set; }
    public required SetDataFromCommunicationFunc SetDataFromCommunication { get; set; }
    public required float XSensitivity { get; set; }
    public required float YSensitivity { get; set; }
    public required float Dpi { get; set; }
    public readonly float XCorrectionMultiplier => 22.0f / XSensitivity * (640.0f / Dpi);
    public readonly float YCorrectionMultiplier => 22.0f / YSensitivity * (480.0f / Dpi);

    public static AimAssistConfiguration GetCs2Configuration()
    {
        FpsAim.Shared.AimAssistSettings aimAssistSettings;

        if (!File.Exists("cs2.json"))
        {
            aimAssistSettings = new(
                ConfidenceThreshold: 0.5f,
                XSensitivity: 1.0f,
                YSensitivity: 1.2f,
                Dpi: 1280.0f,
                Breakpoints: [
                    [0.0f, 0.0f],
                    [0.15f, 0.035f],
                    [0.55f, 0.025f],
                    [1.0f, 0.017f],
                    [1.5f, 0.0066f],
                    [2.0f, 0.0016f],
                    [5.0f, 0.0008f],
                    [100.0f, 0.000001f],
                ],
                TargetBoxXOffset: 0.5f,
                TargetBoxYOffset: 0.3f,
                ProgressFactor: 5.0f
            );

            File.WriteAllText("cs2.json", aimAssistSettings.Serialize());
        }
        else
        {
            aimAssistSettings = FpsAim.Shared.AimAssistSettings.Deserialize(File.ReadAllText("cs2.json"))!;
        }

        var breakpoints = aimAssistSettings.Breakpoints.Select(b => new StickySmoothing.Breakpoint { Distance = b[0], Value = b[1] }).ToArray();

        return new AimAssistConfiguration()
        {
            Engine = CreateAimAssistSession("v2_f16.onnx", AimAssistAccelerator.TensorRT),
            TargetPredictor = new LinearPredictor(),
            SmoothingFunction = new ProgressiveSmoothing(new StickySmoothing(breakpoints), aimAssistSettings.ProgressFactor),
            ActivationCondition = new ToggleActivationCondition(MouseButton.XButton1),
            TargetPointSelector = new OffsetPointSelector(xOffset: aimAssistSettings.TargetBoxXOffset, yOffset: aimAssistSettings.TargetBoxYOffset),
            Targets = [AimAssistTarget.Head],
            ConfidenceThreshold = aimAssistSettings.ConfidenceThreshold,
            CaptureWidth = 320,
            CaptureHeight = 320,
            XSensitivity = aimAssistSettings.XSensitivity,
            YSensitivity = aimAssistSettings.YSensitivity,
            Dpi = aimAssistSettings.Dpi,
            GetDataForCommunication = GetDataForCommunication,
            SetDataFromCommunication = SetDataFromCommunication
        };


        FpsAim.Shared.AimAssistSettings GetDataForCommunication(AimAssistConfiguration config)
        {
            var targetPointSelector = (OffsetPointSelector)config.TargetPointSelector;
            var smoothingFunction = (StickySmoothing)((ProgressiveSmoothing)config.SmoothingFunction).SmoothingFunction;
            return new FpsAim.Shared.AimAssistSettings(
                config.ConfidenceThreshold,
                config.XSensitivity,
                config.YSensitivity,
                config.Dpi,
                smoothingFunction.Breakpoints.Select(b => new float[] { b.Distance, b.Value }).ToArray(),
                targetPointSelector.XOffset,
                targetPointSelector.YOffset,
                ((ProgressiveSmoothing)config.SmoothingFunction).ProgressFactor
            );
        }

        void SetDataFromCommunication(AimAssistConfiguration config, FpsAim.Shared.AimAssistSettings settings)
        {
            var targetPointSelector = (OffsetPointSelector)config.TargetPointSelector;
            var smoothingFunction = (StickySmoothing)((ProgressiveSmoothing)config.SmoothingFunction).SmoothingFunction;
            targetPointSelector.XOffset = settings.TargetBoxXOffset;
            targetPointSelector.YOffset = settings.TargetBoxYOffset;
            smoothingFunction.Breakpoints = settings.Breakpoints.Select(b => new StickySmoothing.Breakpoint { Distance = b[0], Value = b[1] }).ToArray();
            ((ProgressiveSmoothing)config.SmoothingFunction).ProgressFactor = settings.ProgressFactor;
        }

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
