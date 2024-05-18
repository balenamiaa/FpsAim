namespace FpsAim;

public class LogarithmicSmoothing(float baseSmoothing) : ISmoothingFunction
{
    public float Calculate(float dx, float dy)
    {
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        return baseSmoothing / (1.0f + MathF.Log(distance + 1));
    }
}