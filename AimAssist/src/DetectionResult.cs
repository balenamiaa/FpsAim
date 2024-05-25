using System.Numerics;

namespace AimAssist;

public struct DetectionResult
{
    public required uint ClassId;
    public required float Confidence;
    public required float XMin;
    public required float YMin;
    public required float XMax;
    public required float YMax;
    public required uint ImageWidth;
    public required uint ImageHeight;

    public required bool IsPopulated;

    public readonly Vector2 OffsetAbsoluteFromCenter(float targetX, float targetY, float centerX, float centerY)
    {
        float left = centerX - ImageWidth / 2f;
        float top = centerY - ImageHeight / 2f;
        return new Vector2(left + targetX, top + targetY);
    }

    public readonly float GetDistanceUnits(Vector2 p1, Vector2 p2)
    {
        float width = XMax - XMin;
        float height = YMax - YMin;
        float boxMagnitude = MathF.Sqrt(width * width + height * height);

        float dx = p2.X - p1.X;
        float dy = p2.Y - p1.Y;
        float distMagnitude = MathF.Sqrt(dx * dx + dy * dy);

        return distMagnitude / boxMagnitude;
    }
}
