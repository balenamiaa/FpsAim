using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace FpsAim;

public class KalmanFilterPredictor : ITargetPredictor
{
    private readonly Matrix<double> _a; // State transition matrix
    private readonly Matrix<double> _h; // Measurement matrix
    private readonly Matrix<double> _q; // Process noise covariance
    private readonly Matrix<double> _r; // Measurement noise covariance
    private KalmanFilterState _state;

    public KalmanFilterPredictor(float assumedFps = 60.0f, float processNoiseFactor = 0.1f,
        float measurementNoiseFactor = 1.0f)
    {
        var c = 1.0 / assumedFps;

        // Model - Assuming constant velocity
        _a = Matrix.Build.DenseOfArray(new[,]
        {
            { 1, 0, c, 0 },
            { 0, 1, 0, c },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 }
        });

        // Measurement matrix (we're only observing position)
        _h = Matrix.Build.DenseOfArray(new double[,]
        {
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 }
        });

        // Process noise covariance (adjust as needed)
        _q = Matrix.Build.DiagonalIdentity(4) * processNoiseFactor;

        // Measurement noise covariance (adjust based on measurement accuracy)
        _r = Matrix.Build.DiagonalIdentity(2) * measurementNoiseFactor;

        _state = new KalmanFilterState
        {
            X = Vector.Build.Dense(4), // Initial state (assuming target at rest)
            P = Matrix.Build.DiagonalIdentity(4) * 100.0 // Large initial uncertainty
        };
    }

    public (float X, float Y) Predict(float targetX, float targetY, double dt)
    {
        // 1. Prediction
        var predictedX = _a * _state.X;
        var predictedP = _a * _state.P * _a.Transpose() + _q;

        // 2. Measurement Update
        var z = Vector.Build.Dense(new[] { targetX, (double)targetY });
        var y = z - _h * predictedX;
        var s = _h * predictedP * _h.Transpose() + _r;
        var k = predictedP * _h.Transpose() * s.Inverse();
        _state.X = predictedX + k * y;
        _state.P = (Matrix.Build.DiagonalIdentity(4) - k * _h) * predictedP;

        return ((float)_state.X[0], (float)_state.X[1]);
    }

    private record struct KalmanFilterState(Vector<double> X, Matrix<double> P);
}