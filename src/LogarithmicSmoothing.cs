namespace FpsAim;

public class LogarithmicSmoothing(float baseSmoothing) : ISmoothingFunction
{
    public float Calculate(float distance)
    {
        return baseSmoothing / (1.0f + MathF.Log(distance + 1));
    }
}