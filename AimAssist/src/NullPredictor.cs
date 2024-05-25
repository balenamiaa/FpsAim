using System.Numerics;

namespace AimAssist;

public class NullPredictor : ITargetPredictor
{
    public Vector2 Predict(Vector2 targetPosition, float dt) => targetPosition;

    public void Reset() { }
}