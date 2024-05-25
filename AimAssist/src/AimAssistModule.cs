using System.Diagnostics;
using System.Numerics;
using System.Threading.Channels;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Windows.Win32;
using Windows.Win32.System.Threading;
namespace AimAssist;

public class AimAssistModule : IDisposable
{
    private AimAssistConfiguration _config;
    private readonly MouseMover _mouseMover;
    private readonly ScreenCapturer _screenCapturer;
    private readonly Channel<InferenceOutput> _inferenceOutputChannel;
    private readonly CancellationTokenSource _inferenceLoopCts;
    private Task? _inferenceLoopTask;
    private readonly uint _screenWidth;
    private readonly uint _screenHeight;
    private readonly uint[] _classIdTargets;
    private readonly int _numClasses;
    private readonly int _numDetections;
    private readonly OrtValue _onnxOutput;
    private readonly OrtIoBinding _onnxIoBinding;
    private readonly RunOptions _onnxRunOptions;
    private readonly DetectionResult[] _detectionsBuffer = new DetectionResult[1000];
    private readonly DetectionResult[] _activeDetectionsBuffer = new DetectionResult[100];

    public AimAssistModule(AimAssistConfiguration config)
    {
        _config = config;
        _mouseMover = new MouseMover();
        _screenCapturer = new ScreenCapturer(0, 0, config.CaptureWidth, config.CaptureHeight);
        _inferenceOutputChannel = Channel.CreateBounded<InferenceOutput>(64);
        _screenWidth = _screenCapturer.ScreenWidth;
        _screenHeight = _screenCapturer.ScreenHeight;
        _classIdTargets = config.Targets.Select(target => target.ToClassId()).ToArray();
        var outputShape = _config.Engine.OutputMetadata.First().Value.Dimensions!;
        _numClasses = outputShape[1] - 4;
        _numDetections = outputShape[2];
        _onnxOutput = OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, TensorElementType.Float,
            outputShape.Select(x => (long)x).ToArray());
        _onnxIoBinding = config.Engine.CreateIoBinding();
        _onnxIoBinding.BindOutput("output0", _onnxOutput);
        _onnxRunOptions = new RunOptions()
        {
            LogId = "AimAssistModule",
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO,
        };
        _inferenceLoopCts = new CancellationTokenSource();
    }

    public async Task RunAsync()
    {

        var thisCopy = this;
        var _inferenceLoopCtsCopy = _inferenceLoopCts;
        _inferenceLoopTask = Task.Run(async () =>
        {
            await thisCopy.RunInferenceLoopAsync(_inferenceLoopCtsCopy.Token);
        });

        var centerX = _screenWidth / 2f;
        var centerY = _screenHeight / 2f;
        var lastDetectionTime = new Stopwatch();
        var loopMetrics = new LoopMetrics();

        await _inferenceOutputChannel.Reader.WaitToReadAsync();
        Console.WriteLine("AimAssist is running...");


        while (!_inferenceLoopCts.IsCancellationRequested)
        {
            _config.ActivationCondition.Update();
            _config.SmoothingFunction.Update();

            var inferenceOutput = await _inferenceOutputChannel.Reader.ReadAsync();
            loopMetrics.Update(inferenceOutput.InferenceTimeMs, inferenceOutput.ScreenCaptureTimeMs);

            if (inferenceOutput.Detections.Length == 0)
                goto loopEnd;

            var classIdTargets = _classIdTargets;
            var detection = Array.Find(inferenceOutput.Detections, d => classIdTargets.Contains(d.ClassId));

            if (!detection.IsPopulated)
                goto loopEnd;

            var targetX = (detection.XMin + detection.XMax) / 2f;
            var targetY = (detection.YMin + detection.YMax) / 2f;
            var predictedPos = _config.TargetPredictor.Predict(new Vector2(targetX, targetY), (float)lastDetectionTime.Elapsed.TotalSeconds);

            if (_config.ActivationCondition.ShouldAimAssist())
            {
                var offsetPos =
                    detection.OffsetAbsoluteFromCenter(predictedPos.X, predictedPos.Y, centerX, centerY);
                var distUnits = detection.GetDistanceUnits(offsetPos, new Vector2(centerX, centerY));

                var dx = offsetPos.X - centerX;
                var dy = offsetPos.Y - centerY;

                var smoothFactor = _config.SmoothingFunction.Calculate(distUnits, 1.0f);

                var dxSmooth = (int)MathF.Ceiling(_config.XCorrectionMultiplier * dx * smoothFactor);
                var dySmooth = (int)MathF.Ceiling(_config.YCorrectionMultiplier * dy * smoothFactor);

                _mouseMover.MoveRelative(dxSmooth, dySmooth);
            }

        loopEnd:
            lastDetectionTime.Restart();
            _config.TargetPredictor.Reset();
            if (loopMetrics.LoopCount % 1000 == 0)
            {
                loopMetrics.PrintAndReset();
            }
        }

        if (_inferenceLoopTask.Exception is { } exception)
        {
            throw exception;
        }
    }

    private async Task RunInferenceLoopAsync(CancellationToken ct)
    {
        uint taskIndex = 0;
        var taskHandle = PInvoke.AvSetMmThreadCharacteristics("Games", ref taskIndex);
        PInvoke.AvSetMmThreadPriority(taskHandle, AVRT_PRIORITY.AVRT_PRIORITY_CRITICAL);

        var writer = _inferenceOutputChannel.Writer;

        var st = Stopwatch.StartNew();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                st.Restart();

                var capturedFrame = _screenCapturer.CaptureFrame();
                var screenCaptureTime = st.ElapsedMilliseconds;
                st.Restart();

                if (capturedFrame is ScreenCaptureOutputNotAvailable)
                {
                    continue;
                }

                using var mappedTensor = ((ScreenCaptureOutputAvailable)capturedFrame).GetGpuMappedTensor();
                var tensor = mappedTensor.Tensor;
                _onnxIoBinding.BindInput("images", tensor);
                _config.Engine.RunWithBinding(_onnxRunOptions, _onnxIoBinding);
                _onnxIoBinding.ClearBoundInputs();
                var detections = ParseOutput(_onnxOutput, _config.ConfidenceThreshold);

                var inferenceTime = st.ElapsedMilliseconds;

                await writer.WriteAsync(new InferenceOutput
                {
                    Detections = NonMaxSuppression(detections.Span, 0.5f).ToArray(),
                    InferenceTimeMs = inferenceTime,
                    ScreenCaptureTimeMs = screenCaptureTime
                }, ct);
            }
        }
        finally
        {
            writer.Complete();
        }


    }

    private Memory<DetectionResult> ParseOutput(OrtValue onnxOutput, float confidenceThreshold)
    {
        var outputData = onnxOutput.GetTensorDataAsSpan<float>();
        var numDetections = _numDetections;

        (int, uint)[] validIndices;
        unsafe
        {
            fixed (float* outputDataPtr = outputData)
            {
                var unsafeOutputDataPtrMustNotOutliveNorEscapeReallyDangerous = outputDataPtr;
                validIndices = Enumerable.Range(4 * numDetections, 2 * numDetections)
                    .AsParallel().Select(
                        index =>
                        {
                            var classId = index / numDetections - 4;
                            var detectionIndex = index % numDetections;

                            if (unsafeOutputDataPtrMustNotOutliveNorEscapeReallyDangerous[index] > confidenceThreshold)
                            {
                                return ((int, uint)?)(detectionIndex, classId);
                            }

                            return null;
                        }).Where(index => index is not null).Select(index => index!.Value).ToArray();
            }
        }


        var detectionCount = 0;
        foreach (var (j, classId) in validIndices)
        {
            var xCenter = outputData[j];
            var yCenter = outputData[j + numDetections];
            var width = outputData[j + 2 * numDetections];
            var height = outputData[j + 3 * numDetections];
            var confidence = outputData[(int)(j + (4 + classId) * numDetections)];

            _detectionsBuffer[detectionCount++] = new DetectionResult
            {
                ClassId = classId,
                Confidence = confidence,
                XMin = xCenter - width / 2,
                YMin = yCenter - height / 2,
                XMax = xCenter + width / 2,
                YMax = yCenter + height / 2,
                ImageWidth = _screenCapturer.CaptureWidth,
                ImageHeight = _screenCapturer.CaptureHeight,
                IsPopulated = true
            };
        }

        return _detectionsBuffer.AsMemory()[..detectionCount];
    }

    private Memory<DetectionResult> NonMaxSuppression(Span<DetectionResult> detections, float iouThreshold)
    {
        if (detections.Length == 0)
            return Memory<DetectionResult>.Empty;


        detections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

        Span<bool> active = stackalloc bool[detections.Length];
        active.Fill(true);

        for (var i = 0; i < detections.Length; i++)
        {
            if (!active[i])
                continue;

            var detection = detections[i];

            for (var j = i + 1; j < detections.Length; j++)
            {
                if (!active[j])
                    continue;

                if (GetIoU(detection, detections[j]) >= iouThreshold)
                {
                    active[j] = false;
                }
            }
        }


        var k = 0;
        for (var i = 0; i < detections.Length; i++)
        {
            if (active[i])
            {
                _activeDetectionsBuffer[k++] = detections[i];
            }
        }


        return _activeDetectionsBuffer.AsMemory()[..k];
    }

    private static float GetIoU(DetectionResult box1, DetectionResult box2)
    {
        var xMin = MathF.Max(box1.XMin, box2.XMin);
        var yMin = MathF.Max(box1.YMin, box2.YMin);
        var xMax = MathF.Min(box1.XMax, box2.XMax);
        var yMax = MathF.Min(box1.YMax, box2.YMax);

        var interArea = MathF.Max(0, xMax - xMin) * MathF.Max(0, yMax - yMin);
        var box1Area = (box1.XMax - box1.XMin) * (box1.YMax - box1.YMin);
        var box2Area = (box2.XMax - box2.XMin) * (box2.YMax - box2.YMin);
        var unionArea = box1Area + box2Area - interArea;

        return interArea / unionArea;
    }

    public void Dispose()
    {
        _inferenceLoopCts.Cancel();
        _screenCapturer.Dispose();
        _mouseMover.Dispose();
    }
}