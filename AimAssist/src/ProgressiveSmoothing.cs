

using System.Diagnostics;

namespace AimAssist;

internal class ProgressiveSmoothing(ISmoothingFunction SmoothingFunction, float ProgressFactor) : ISmoothingFunction
{
    private bool _wasLeftClickDown;
    private readonly Stopwatch _leftClickDuration = new();

    public float Calculate(float distance, float dt)
    {
        float tLeftClickDown = (float)_leftClickDuration.Elapsed.TotalSeconds;
        float smoothing = SmoothingFunction.Calculate(distance, dt);
        return smoothing / (1.0f + ProgressFactor * tLeftClickDown);
    }

    public void Update()
    {
        SmoothingFunction.Update();

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