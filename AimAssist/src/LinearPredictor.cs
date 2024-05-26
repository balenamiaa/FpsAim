using System.Numerics;

namespace AimAssist;

internal class LinearPredictor : ITargetPredictor
{
    private State _state;

    public Vector2 Predict(Vector2 targetPosition, float dt)
    {
        var velocity = (targetPosition - _state.Position) / dt;
        var acceleration = (velocity - _state.Velocity) / dt;

        _state = new State(targetPosition, velocity);

        return targetPosition + velocity * dt + acceleration * dt * dt / 2;
    }

    public void Reset()
    {
        _state = new State(Vector2.Zero, Vector2.Zero);
    }


    private record struct State(Vector2 Position, Vector2 Velocity);
}