namespace FpsAim;

public class SigmoidSmoothing(float steepness, float cap, float m, float n) : ISmoothingFunction
{
    public float Calculate(float distance)
    {
        return cap / (1f + m * (float)Math.Pow(n, steepness * Math.Sqrt(distance)));
    }
}