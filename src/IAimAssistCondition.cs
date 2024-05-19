namespace FpsAim;

public interface IAimAssistCondition
{
    public bool ShouldAimAssist();
    void Update();
}