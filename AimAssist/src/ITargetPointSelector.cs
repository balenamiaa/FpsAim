using System.Numerics;

namespace AimAssist;

public interface ITargetPointSelector
{
    public Vector2 SelectPoint(float xMin, float yMin, float xMax, float yMax);
}