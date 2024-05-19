namespace FpsAim;

internal class ClickOnceActivationCondition(ClickOnceActivationCondition.ClickMouseButton button) : IAimAssistCondition
{
    private State _state = new(false);

    public enum ClickMouseButton
    {
        Mouse4,
        Mouse5
    }

    private bool IsDown => button switch
    {
        ClickMouseButton.Mouse4 => MouseMover.IsMouse4Down(),
        ClickMouseButton.Mouse5 => MouseMover.IsMouse5Down(),
        _ => throw new ArgumentOutOfRangeException()
    };

    public bool ShouldAimAssist()
    {
        var result = IsDown && !_state.WasPreviouslyDown;
        return result;
    }

    public void Update()
    {
        _state = new State(IsDown);
    }


    private readonly record struct State(bool WasPreviouslyDown);
}