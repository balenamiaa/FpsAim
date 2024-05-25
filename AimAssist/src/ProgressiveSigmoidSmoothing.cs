using System.Diagnostics;

namespace AimAssist;

public class ProgressiveSigmoidSmoothing(float steepness, float cap, float m, float n, float progressFactor) : ISmoothingFunction
{
    private readonly float _steepness = steepness;
    private readonly float _cap = cap;
    private readonly float _m = m;
    private readonly float _n = n;
    private readonly float _progressFactor = progressFactor;
    private bool _wasLeftClickDown;
    private Stopwatch _leftClickDuration = new();

    public float Calculate(float distance, float dt)
    {
        if (distance == 0.0f) return 0.0f;

        float tLeftClickDown = (float)_leftClickDuration.Elapsed.TotalSeconds;
        float sigmoidFactor = _cap / (_m * MathF.Pow(_n, _steepness * MathF.Sqrt(distance)));
        return sigmoidFactor * dt / (1.0f + _progressFactor * tLeftClickDown);
    }

    public void Update()
    {
        bool isLeftClickDown = MouseInterop.IsButtonDown(MouseButton.Left);

        if (isLeftClickDown && !_wasLeftClickDown)
        {
            _leftClickDuration.Restart();
        }
        else if (!isLeftClickDown && _wasLeftClickDown)
        {
            _leftClickDuration.Reset();
        }

        _wasLeftClickDown = isLeftClickDown;
    }
}
