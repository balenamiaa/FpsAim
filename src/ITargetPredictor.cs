namespace FpsAim;

public interface ITargetPredictor
{
    (float X, float Y) Predict(float targetX, float targetY, double dt);
}