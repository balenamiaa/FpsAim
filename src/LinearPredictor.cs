namespace FpsAim;

public class LinearPredictor : ITargetPredictor
{
    private LinearPredictorState? State { get; set; }

    public (float X, float Y) Predict(float targetX, float targetY, double dt)
    {
        switch (State)
        {
            case null:
                State = new LinearPredictorState(targetX, targetY);
                return (targetX, targetY);
            case { } state:
                var deltaX = targetX - state.PreviousX;
                var deltaY = targetY - state.PreviousY;
                var predictedX = targetX + (float)(deltaX * dt);
                var predictedY = targetY + (float)(deltaY * dt);
                State = new LinearPredictorState(targetX, targetY);
                return (predictedX, predictedY);
        }
    }


    private record struct LinearPredictorState(float PreviousX, float PreviousY);
}