namespace AimAssist;

public static class AimAssistTargetExtensions
{
    public static uint ToClassId(this AimAssistTarget target)
    {
        return target switch
        {
            AimAssistTarget.Head => 0,
            AimAssistTarget.Torso => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
    }
}   