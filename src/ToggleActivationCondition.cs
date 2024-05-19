namespace FpsAim;

public class ToggleActivationCondition(ToggleActivationCondition.ToggleMouseButton toggleMouseButton)
    : IAimAssistCondition
{
    public enum ToggleMouseButton
    {
        Mouse4,
        Mouse5
    }


    private State _state = new(false, false);

    private bool IsDown => toggleMouseButton switch
    {
        ToggleMouseButton.Mouse4 => MouseMover.IsMouse4Down(),
        ToggleMouseButton.Mouse5 => MouseMover.IsMouse5Down(),
        _ => throw new ArgumentOutOfRangeException()
    };

    public bool ShouldAimAssist()
    {
        return _state.IsToggled;
    }

    public void Update()
    {
        if (IsDown && !_state.WasPreviouslyDown)
            _state.IsToggled = !_state.IsToggled;

        _state.WasPreviouslyDown = IsDown;
    }

    private record struct State(bool WasPreviouslyDown, bool IsToggled);
}