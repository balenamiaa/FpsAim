namespace AimAssist;

public interface ISmoothingFunction
{
    float Calculate(float distance, float dt);
    void Update();  
}