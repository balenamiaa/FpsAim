namespace AimAssist;
public class Program
{
    public static async Task Main(string[] args)
    {
        var config = AimAssistConfiguration.GetCs2Configuration();
        using var aimAssistModule = new AimAssistModule(config);
        await aimAssistModule.RunAsync();
    }
}