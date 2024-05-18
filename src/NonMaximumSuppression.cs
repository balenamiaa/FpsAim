namespace FpsAim;

public static class NonMaximumSuppression
{
    public static List<DetectionResult> Run(List<DetectionResult> detections, float iouThreshold)
    {
        detections = detections.OrderByDescending(d => d.Confidence).ToList();

        var selectedDetections = new List<DetectionResult>();
        var isSuppressed = new bool[detections.Count];

        for (var i = 0; i < detections.Count; i++)
        {
            if (isSuppressed[i]) continue;

            selectedDetections.Add(detections[i]);

            for (var j = i + 1; j < detections.Count; j++)
                if (Iou(detections[i], detections[j]) > iouThreshold)
                    isSuppressed[j] = true;
        }

        return selectedDetections;
    }

    private static float Iou(DetectionResult box1, DetectionResult box2)
    {
        var x1 = Math.Max(box1.XMin, box2.XMin);
        var y1 = Math.Max(box1.YMin, box2.YMin);
        var x2 = Math.Min(box1.XMax, box2.XMax);
        var y2 = Math.Min(box1.YMax, box2.YMax);

        var intersection = Math.Max(x2 - x1, 0) * Math.Max(y2 - y1, 0);
        var box1Area = (box1.XMax - box1.XMin) * (box1.YMax - box1.YMin);
        var box2Area = (box2.XMax - box2.XMin) * (box2.YMax - box2.YMin);

        return intersection / (box1Area + box2Area - intersection);
    }
}