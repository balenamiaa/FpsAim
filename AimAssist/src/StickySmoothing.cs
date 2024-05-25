
namespace AimAssist;



internal class StickySmoothing(StickySmoothing.Breakpoint[] breakpoints) : ISmoothingFunction
{

    public float Calculate(float distance, float dt)
    {
        if (distance == 0.0f) return 0.0f;

        float value = 0.0f;
        float lastDistance = 0.0f;
        float lastValue = 0.0f;

        foreach (var breakpoint in breakpoints)
        {
            if (distance < breakpoint.Distance)
            {
                float t = (distance - lastDistance) / (breakpoint.Distance - lastDistance);
                value = Lerp(lastValue, breakpoint.Value, t);
                break;
            }

            lastDistance = breakpoint.Distance;
            lastValue = breakpoint.Value;
        }

        return value;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public void Update()
    {
    }

    internal struct Breakpoint
    {
        public required float Distance { get; set; }
        public required float Value { get; set; }
    }
}