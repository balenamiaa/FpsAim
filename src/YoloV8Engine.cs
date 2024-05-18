using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FpsAim;

// ReSharper disable once InconsistentNaming
public sealed class YoloV8Engine : InferenceEngine
{
    private readonly DenseTensor<byte> _inputTensor;

    public YoloV8Engine(string modelPath, SessionOptions sessionOptions) : base(modelPath, sessionOptions)
    {
        var inputMeta = Session.InputMetadata;
        Debug.Assert(inputMeta.Count == 1);
        Debug.Assert(inputMeta.First().Key == "images");
        Debug.Assert(inputMeta.First().Value.Dimensions.Take(4).SequenceEqual([1, 3, ImageWidth, ImageHeight]));
        _inputTensor = new DenseTensor<byte>([1, 3, ImageWidth, ImageHeight]);
    }

    protected override string[] Classes => ["enemy_head", "enemy_torso"];


    public override void Infer(ReadOnlySpan<byte> input, int width, int height)
    {
        base.Infer(input, width, height);
        ProcessImageFromBGRAInto_U8RGB(input, _inputTensor, width, height);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", _inputTensor)
        };

        using var outputs = Session.Run(inputs);
        ParseOutput(outputs);
    }


    private void ParseOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output)
    {
        var boxes = output.First(o => o.Name == "output0").AsTensor<float>();

        var numPredictions = boxes.Dimensions[1];

        for (var i = 0; i < numPredictions; i++)
        {
            var classId = boxes[0, i, 5];
            var confidence = boxes[0, i, 4];
            var xMin = boxes[0, i, 0];
            var yMin = boxes[0, i, 1];
            var xMax = boxes[0, i, 2];
            var yMax = boxes[0, i, 3];

            DetectionsBuffer[i] = new DetectionResult
            {
                ClassId = (long)classId,
                Confidence = confidence,
                XMin = xMin,
                YMin = yMin,
                XMax = xMax,
                YMax = yMax
            };
        }
    }
}