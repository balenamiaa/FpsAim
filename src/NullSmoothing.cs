namespace FpsAim;

public class NullSmoothing : ISmoothingFunction
{
    public float Calculate(float _)
    {
        return 1.0f;
    }
}