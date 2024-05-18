using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FpsAim;

// ReSharper disable once InconsistentNaming
public sealed class YoloNASEngine : InferenceEngine
{
    private readonly DenseTensor<byte> _inputTensor;

    public YoloNASEngine(string modelPath, SessionOptions sessionOptions) : base(modelPath, sessionOptions)
    {
        var inputMeta = Session.InputMetadata;
        Debug.Assert(inputMeta.Count == 1);
        Debug.Assert(inputMeta.First().Key == "input");
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
            NamedOnnxValue.CreateFromTensor("input", _inputTensor)
        };

        using var outputs = Session.Run(inputs);
        ParseOutput(outputs);
    }


    private void ParseOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output)
    {
        // Extract the number of predictions
        var numPredictions = (int)output.First(o => o.Name == "graph2_num_predictions")
            .AsTensor<long>().GetValue(0);

        // Extract bounding boxes
        var predBoxes = output.First(o => o.Name == "graph2_pred_boxes")
            .AsTensor<float>();

        // Extract scores
        var predScores = output.First(o => o.Name == "graph2_pred_scores")
            .AsTensor<float>();

        // Extract class IDs
        var predClasses = output.First(o => o.Name == "graph2_pred_classes")
            .AsTensor<long>();

        for (var i = 0; i < numPredictions; i++)
        {
            var xMin = predBoxes[0, i, 0];
            var yMin = predBoxes[0, i, 1];
            var xMax = predBoxes[0, i, 2];
            var yMax = predBoxes[0, i, 3];

            DetectionsBuffer[i] = new DetectionResult
            {
                ClassId = predClasses[0, i],
                Confidence = predScores[0, i],
                XMin = xMin,
                YMin = yMin,
                XMax = xMax,
                YMax = yMax
            };
        }
    }
}