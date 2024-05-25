using System.Diagnostics;

namespace AimAssist;

public class SigmoidSmoothing(float steepness, float cap, float m, float n) : ISmoothingFunction
{
    private readonly float _steepness = steepness;
    private readonly float _cap = cap;
    private readonly float _m = m;
    private readonly float _n = n;

    public float Calculate(float distance, float dt)
    {
        if (distance == 0.0f) return 0.0f;


        return dt * _cap / (_m * MathF.Pow(_n, _steepness * MathF.Sqrt(distance)));
    }

    public void Update()
    {
    }
}
