namespace FpsAim;

// MouseButton param ignored because so far we only support LButton
internal class KeyNotDownActivationCondition(KeyNotDownActivationCondition.MouseButton _)
    : IAimAssistCondition
{
    public enum MouseButton
    {
        LButton
    }

    private State _state;

    public bool ShouldAimAssist()
    {
        return !_state.IsKeyDown[(int)MouseButton.LButton];
    }

    public void Update()
    {
        _state = MouseMover.IsLeftMouseButtonDown()
            ? new State { IsKeyDown = [true] }
            : new State { IsKeyDown = [false] };
    }


    private record struct State
    {
        public bool[] IsKeyDown { get; init; }
    }
}