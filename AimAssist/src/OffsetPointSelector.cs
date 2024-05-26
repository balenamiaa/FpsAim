using System.Numerics;

namespace AimAssist;

internal class OffsetPointSelector(float xOffset, float yOffset) : ITargetPointSelector
{
    public float XOffset {
        get => xOffset;
        set => xOffset = value;
    }
    public float YOffset {
        get => yOffset;
        set => yOffset = value;
    }

    public Vector2 SelectPoint(float xMin, float yMin, float xMax, float yMax)
    {
        return new Vector2(xMin + (xMax - xMin) * xOffset, yMin + (yMax - yMin) * yOffset);
    }
}