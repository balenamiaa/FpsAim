using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FpsAim;

// ReSharper disable once InconsistentNaming
public sealed class YoloV8Engine : InferenceEngine
{
    private readonly int MaxClassIndex;
    private readonly int MaxDetections;

    public YoloV8Engine(string modelPath, SessionOptions sessionOptions) : base(modelPath, sessionOptions)
    {
        var inputMeta = Session.InputMetadata;
        var outputMeta = Session.OutputMetadata;
        Debug.Assert(inputMeta.Count == 1);
        Debug.Assert(inputMeta.First().Key == "images");
        MaxClassIndex = outputMeta.First().Value.Dimensions[1];
        MaxDetections = outputMeta.First().Value.Dimensions[2];
    }


    public override void Infer(Memory<float> input, int width, int height, float confidenceThreshold)
    {
        base.Infer(input, width, height, confidenceThreshold);
        var tensor = new DenseTensor<float>(input.ToArray(), [1, 3, InputHeight, InputWidth]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", tensor)
        };

        using var outputs = Session.Run(inputs);
        ParseOutput(outputs, confidenceThreshold);
    }


    private void ParseOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output, float confidenceThreshold)
    {
        var boxes = output.First(o => o.Name == "output0").AsTensor<float>();
        Parallel.For(0, MaxDetections, i =>
        {
            var xCenter = boxes[0, 0, i];
            var yCenter = boxes[0, 1, i];
            var width = boxes[0, 2, i];
            var height = boxes[0, 3, i];

            for (var j = 4; j < MaxClassIndex; j++)
            {
                var confidenceValue = boxes[0, j, i];
                if (confidenceValue < confidenceThreshold) continue;

                DetectionsBuffer[i + j] = new DetectionResult
                {
                    ClassId = j - 4,
                    Confidence = confidenceValue,
                    XMin = xCenter - width / 2,
                    YMin = yCenter - height / 2,
                    XMax = xCenter + width / 2,
                    YMax = yCenter + height / 2,
                    Width = InputWidth,
                    Height = InputHeight
                };
            }
        });
    }
}