namespace FpsAim;

public class SigmoidSmoothing(float steepness, float cap, float m, float D) : ISmoothingFunction
{
    public float Calculate(float dx, float dy)
    {
        var distance = MathF.Sqrt(dx * dx + dy * dy) / D;
        return cap / (1f + m * (float)Math.Exp(steepness * distance));
    }
}