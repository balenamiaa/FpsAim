using System.Runtime.InteropServices;

namespace AimAssist;

internal static partial class MouseInterop
{
    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);

    public static bool IsButtonDown(MouseButton button) => GetAsyncKeyState((int)button) != 0;
        
}
