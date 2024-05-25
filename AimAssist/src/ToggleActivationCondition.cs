namespace AimAssist;

public class ToggleActivationCondition(MouseButton toggleButton) : IActivationCondition
{
    private readonly MouseButton _toggleButton = toggleButton;
    private bool _wasButtonDown;
    private bool _isToggled;

    public bool ShouldAimAssist() => _isToggled;

    public void Update()
    {
        bool isButtonDown = MouseInterop.IsButtonDown(_toggleButton);

        if (isButtonDown && !_wasButtonDown)
        {
            _isToggled = !_isToggled;
        }

        _wasButtonDown = isButtonDown;
    }
}