namespace AimAssist;

public interface IActivationCondition
{
    bool ShouldAimAssist();
    void Update();
}
