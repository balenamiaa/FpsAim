namespace FpsAim;

public class NullPredictor : ITargetPredictor
{
    public (float X, float Y) Predict(float targetX, float targetY, double _)
    {
        return (targetX, targetY);
    }
}