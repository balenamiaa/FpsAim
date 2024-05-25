using System.Numerics;

namespace AimAssist;


public interface ITargetPredictor
{
    Vector2 Predict(Vector2 targetPosition, float dt);
    void Reset();
}